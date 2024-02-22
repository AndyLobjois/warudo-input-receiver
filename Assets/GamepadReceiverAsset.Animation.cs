using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Warudo.Core;
using Warudo.Core.Attributes;
using Warudo.Core.Data;
using Warudo.Core.Graphs;
using Warudo.Core.Localization;
using Warudo.Core.Utils;
using Warudo.Plugins.Core.Assets.Prop;
using Warudo.Plugins.Core.Nodes.Event;
using static FlameStream.GamepadReceiverAsset;
using static Warudo.Plugins.Core.Assets.Character.CharacterAsset;

namespace FlameStream {
    public partial class GamepadReceiverAsset : ReceiverAsset {

        void GenerateButtonAnimationTemplate() {
            switch (TargetControllerType) {
                case ControllerType.SwitchProController:
                default:
                    ButtonAnimationData = Enum.GetValues(typeof(SwitchProButton))
                        .Cast<SwitchProButton>()
                        .Where(e => e != SwitchProButton.None)
                        .Select(e => {
                            var old = Array.Find(ButtonAnimationData, d => (SwitchProButton)d.ButtonId == e);

                            var o = StructuredData.Create<GamepadButtonAnimationData>();
                            o.TargetControllerType = ControllerType.SwitchProController;
                            o.ButtonId = (int)e;
                            o.PropLayerName = old?.PropLayerName ?? e.ToString();
                            o.FingerHoverAnimation = old?.FingerHoverAnimation ?? null;
                            o.FingerPressAnimation = old?.FingerPressAnimation ?? null;
                            o.MaskRight = old?.MaskRight ?? false;
                            return o;
                        })
                        .ToArray();
                    break;
            }

            ControlPadAnimationData = Enum.GetValues(typeof(ControlPadDirection))
                .Cast<ControlPadDirection>()
                .Where(e => e != ControlPadDirection.None && e != ControlPadDirection.Neutral)
                .Select(e => {
                    var old = Array.Find(ControlPadAnimationData, d => (ControlPadDirection)d.ButtonId == e);

                    var o = StructuredData.Create<GamepadControlPadAnimationData>();
                    o.ButtonId = (int)e;
                    o.PropLayerName = old?.PropLayerName ?? $"D{(int)e}";
                    o.FingerHoverAnimation = old?.FingerHoverAnimation ?? null;
                    o.FingerPressAnimation = old?.FingerPressAnimation ?? null;
                    return o;
                })
                .ToArray();

            BroadcastDataInput(nameof(ButtonAnimationData));
            BroadcastDataInput(nameof(ControlPadAnimationData));
            Context.Service.PromptMessage("SUCCESS", $@"Button animation definition for [{TargetControllerType}] has been succesfully generated.

Please note that they do not have to be all filled. You may remove unused fields to optimize your setup.
");
        }

        /// <summary>
        /// Ideally this should not be needed and instead we'd use the transient stack.
        /// But we don't have control over it; it may be cleared by somethign else.
        /// So until we have a reserved stack that can be self-managed, this will have to do.
        /// </summary>
        void GenerateAnimationBlueprint() {

            Graph graph = new Graph
            {
                Name = "🔥🎮 Animators",
                Enabled = true
            };

            var receiverNode = graph.AddNode<GetGamepadReceiverDataNode>();
            receiverNode.Receiver = this;

            OnUpdateNode onUpdateLeftFingerButtonNode = graph.AddNode<OnUpdateNode>();
            OnUpdateNode onUpdateRightFingerButtonNode = graph.AddNode<OnUpdateNode>();
            OnUpdateNode onUpdateOtherFingerButtonNode = graph.AddNode<OnUpdateNode>();
            OnUpdateNode onUpdateLeftPropButtonNode = graph.AddNode<OnUpdateNode>();
            OnUpdateNode onUpdateRightPropButtonNode = graph.AddNode<OnUpdateNode>();
            OnUpdateNode onUpdateOtherPropButtonNode = graph.AddNode<OnUpdateNode>();

            GamepadFingerButtonAnimatorNode prevLeftFingerAnimatorNode = null;
            GamepadFingerButtonAnimatorNode prevRightFingerAnimatorNode = null;
            GamepadFingerButtonAnimatorNode prevOtherFingerAnimatorNode = null;
            GamepadPropButtonAnimatorNode prevLeftPropButtonNode = null;
            GamepadPropButtonAnimatorNode prevRightPropButtonNode = null;
            GamepadPropButtonAnimatorNode prevOtherPropButtonNode = null;

            foreach(var d in ButtonAnimationData) {
                var buttonName = d.ButtonName;
                var fingerAnimatorNode = graph.AddNode<GamepadFingerButtonAnimatorNode>();
                fingerAnimatorNode.Character = Character;
                fingerAnimatorNode.InputId = buttonName;
                fingerAnimatorNode.HoverLayerId = d.HoverLayerId;
                fingerAnimatorNode.PressLayerId = d.PressLayerId;
                fingerAnimatorNode.HoverInputId = fingerAnimatorNode.InputId;

                var propButtonAnimatorNode = graph.AddNode<GamepadPropButtonAnimatorNode>();
                propButtonAnimatorNode.Controller = Gamepad;
                propButtonAnimatorNode.PressLayerId = buttonName;

                AddDataConnection(graph, receiverNode, buttonName, fingerAnimatorNode, "IsPressed");
                AddDataConnection(graph, receiverNode, buttonName, propButtonAnimatorNode, "IsPressed");

                var leftFaceButton = LeftFaceButtonIdsSwitch.FirstOrDefault(e => { return (int)e == d.ButtonId; });
                var rightFaceButton = RightFaceButtonIdsSwitch.FirstOrDefault(e => { return (int)e == d.ButtonId; });

                if (leftFaceButton != 0) {
                    AddDataConnection(
                        graph,
                        (Node)prevLeftFingerAnimatorNode ?? receiverNode,
                        prevLeftFingerAnimatorNode == null ? "LeftFaceHoverInputId" : "_HoverInputId",
                        fingerAnimatorNode,
                        "HoverInputId"
                    );

                    AddFlowConnection(graph, (Node)prevLeftFingerAnimatorNode ?? onUpdateLeftFingerButtonNode, "Exit", fingerAnimatorNode, "Enter");
                    AddFlowConnection(graph, (Node)prevLeftPropButtonNode ?? onUpdateLeftPropButtonNode, "Exit", propButtonAnimatorNode, "Enter");
                    prevLeftFingerAnimatorNode = fingerAnimatorNode;
                    prevLeftPropButtonNode = propButtonAnimatorNode;

                } else if (rightFaceButton != 0) {

                    AddDataConnection(
                        graph,
                        (Node)prevRightFingerAnimatorNode ?? receiverNode,
                        prevRightFingerAnimatorNode == null ? "RightFaceHoverInputId" : "_HoverInputId",
                        fingerAnimatorNode,
                        "HoverInputId"
                    );

                    AddFlowConnection(graph, (Node)prevRightFingerAnimatorNode ?? onUpdateRightFingerButtonNode, "Exit", fingerAnimatorNode, "Enter");
                    AddFlowConnection(graph, (Node)prevRightPropButtonNode ?? onUpdateRightPropButtonNode, "Exit", propButtonAnimatorNode, "Enter");
                    prevRightFingerAnimatorNode = fingerAnimatorNode;
                    prevRightPropButtonNode = propButtonAnimatorNode;

                } else {

                    AddFlowConnection(graph, (Node)prevOtherFingerAnimatorNode ?? onUpdateOtherFingerButtonNode, "Exit", fingerAnimatorNode, "Enter");
                    AddFlowConnection(graph, (Node)prevOtherPropButtonNode ?? onUpdateOtherPropButtonNode, "Exit", propButtonAnimatorNode, "Enter");
                    prevOtherFingerAnimatorNode = fingerAnimatorNode;
                    prevOtherPropButtonNode = propButtonAnimatorNode;
                }
            }

            OnUpdateNode onUpdateControlPadNode = graph.AddNode<OnUpdateNode>();

            var fingerControlPadAnimatorNode = graph.AddNode<GamepadFingerControlPadAnimatorNode>();
            fingerControlPadAnimatorNode.Character = Character;
            fingerControlPadAnimatorNode.D1HoverLayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 1)?.HoverLayerId;
            fingerControlPadAnimatorNode.D1PressLayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 1)?.PressLayerId;
            fingerControlPadAnimatorNode.D2HoverLayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 2)?.HoverLayerId;
            fingerControlPadAnimatorNode.D2PressLayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 2)?.PressLayerId;
            fingerControlPadAnimatorNode.D3HoverLayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 3)?.HoverLayerId;
            fingerControlPadAnimatorNode.D3PressLayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 3)?.PressLayerId;
            fingerControlPadAnimatorNode.D4HoverLayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 4)?.HoverLayerId;
            fingerControlPadAnimatorNode.D4PressLayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 4)?.PressLayerId;
            fingerControlPadAnimatorNode.D6HoverLayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 6)?.HoverLayerId;
            fingerControlPadAnimatorNode.D6PressLayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 6)?.PressLayerId;
            fingerControlPadAnimatorNode.D7HoverLayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 7)?.HoverLayerId;
            fingerControlPadAnimatorNode.D7PressLayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 7)?.PressLayerId;
            fingerControlPadAnimatorNode.D8HoverLayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 8)?.HoverLayerId;
            fingerControlPadAnimatorNode.D8PressLayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 8)?.PressLayerId;
            fingerControlPadAnimatorNode.D9HoverLayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 9)?.HoverLayerId;
            fingerControlPadAnimatorNode.D9PressLayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 9)?.PressLayerId;
            AddDataConnection(graph, receiverNode, "LeftFaceHoverInputId", fingerControlPadAnimatorNode, "HoverInputId");
            AddDataConnection(graph, receiverNode, "ControlPad", fingerControlPadAnimatorNode, "ControlPadState");
            AddFlowConnection(graph, onUpdateControlPadNode, "Exit", fingerControlPadAnimatorNode, "Enter");

            var propControlPadAnimatorNode = graph.AddNode<GamepadControlPadAnimatorNode>();
            propControlPadAnimatorNode.Controller = Gamepad;
            propControlPadAnimatorNode.D1LayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 1)?.PropLayerName;
            propControlPadAnimatorNode.D2LayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 2)?.PropLayerName;
            propControlPadAnimatorNode.D3LayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 3)?.PropLayerName;
            propControlPadAnimatorNode.D4LayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 4)?.PropLayerName;
            propControlPadAnimatorNode.D6LayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 6)?.PropLayerName;
            propControlPadAnimatorNode.D7LayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 7)?.PropLayerName;
            propControlPadAnimatorNode.D8LayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 8)?.PropLayerName;
            propControlPadAnimatorNode.D9LayerId = ControlPadAnimationData.FirstOrDefault(d => d.ButtonId == 9)?.PropLayerName;
            AddDataConnection(graph, receiverNode, "ControlPad", fingerControlPadAnimatorNode, "ControlPadState");
            AddFlowConnection(graph, fingerControlPadAnimatorNode, "Exit", propControlPadAnimatorNode, "Enter");

            base.Scene.AddGraph(graph);
            Context.Service.PromptMessage("SUCCESS", $"Blueprint {graph.Name} has been succesfully generated.");
            Context.Service.BroadcastOpenedScene();
            Context.Service.NavigateToGraph(graph.Id, receiverNode.Id);
        }

        void AddFlowConnection(Graph graph, Node n1, string s1, Node n2, string s2) {
            UnityEngine.Debug.Log($"[FlameStream.Asset.GamepadReceiver.Animation] FLOW {n1.GetType().Name}#{s1} => {n2.GetType().Name}#{s2}");
            graph.AddFlowConnection(n1, s1, n2, s2);
        }

        void AddDataConnection(Graph graph, Node n1, string s1, Node n2, string s2) {
            UnityEngine.Debug.Log($"[FlameStream.Asset.GamepadReceiver.Animation] DATA {n1.GetType().Name}#{s1} => {n2.GetType().Name}#{s2}");
            graph.AddDataConnection(n1, s1, n2, s2);
        }

        void SyncCharacterOverlayingAnimations() {

            var userLayers = Character.OverlappingAnimations.Where(d => !d.CustomLayerID.StartsWith("🔥🎮")).ToList();
            foreach (var d in ButtonAnimationData) {
                if (d.FingerHoverAnimation != null) {
                    var hoverLayer = StructuredData.Create<OverlappingAnimationData>();
                    hoverLayer.Animation = d.FingerHoverAnimation;
                    hoverLayer.Weight = 1f;
                    hoverLayer.Speed = 1f;
                    hoverLayer.Masked = true;
                    hoverLayer.MaskedBodyParts = d.MaskRight
                        ? new AnimationMaskedBodyPart[] { AnimationMaskedBodyPart.RightFingers }
                        : new AnimationMaskedBodyPart[] { AnimationMaskedBodyPart.LeftFingers };
                    hoverLayer.Additive = true;
                    hoverLayer.Looping = false;
                    hoverLayer.CustomLayerID = d.HoverLayerId;

                    userLayers.Add(hoverLayer);
                }

                if (d.FingerPressAnimation != null) {
                    var pressLayer = StructuredData.Create<OverlappingAnimationData>();
                    pressLayer.Animation = d.FingerPressAnimation;
                    pressLayer.Weight = 1f;
                    pressLayer.Speed = 1f;
                    pressLayer.Masked = true;
                    pressLayer.MaskedBodyParts = d.MaskRight
                        ? new AnimationMaskedBodyPart[] { AnimationMaskedBodyPart.RightFingers }
                        : new AnimationMaskedBodyPart[] { AnimationMaskedBodyPart.LeftFingers };
                    pressLayer.Additive = true;
                    pressLayer.Looping = false;
                    pressLayer.CustomLayerID = d.PressLayerId;

                    userLayers.Add(pressLayer);
                }
            }

            foreach (var d in ControlPadAnimationData) {
                if (d.FingerHoverAnimation != null) {
                    var hoverLayer = StructuredData.Create<OverlappingAnimationData>();
                    hoverLayer.Animation = d.FingerHoverAnimation;
                    hoverLayer.Weight = 1f;
                    hoverLayer.Speed = 1f;
                    hoverLayer.Masked = true;
                    hoverLayer.MaskedBodyParts = new AnimationMaskedBodyPart[] {
                        AnimationMaskedBodyPart.RightFingers,
                        AnimationMaskedBodyPart.LeftFingers
                    };
                    hoverLayer.Additive = true;
                    hoverLayer.Looping = false;
                    hoverLayer.CustomLayerID = d.HoverLayerId;

                    userLayers.Add(hoverLayer);
                }

                if (d.FingerPressAnimation != null) {
                    var pressLayer = StructuredData.Create<OverlappingAnimationData>();
                    pressLayer.Animation = d.FingerPressAnimation;
                    pressLayer.Weight = 1f;
                    pressLayer.Speed = 1f;
                    pressLayer.Masked = true;
                    pressLayer.MaskedBodyParts = new AnimationMaskedBodyPart[] {
                        AnimationMaskedBodyPart.RightFingers,
                        AnimationMaskedBodyPart.LeftFingers
                    };
                    pressLayer.Additive = true;
                    pressLayer.Looping = false;
                    pressLayer.CustomLayerID = d.PressLayerId;

                    userLayers.Add(pressLayer);
                }
            }

            UnityEngine.Debug.Log("[GamepadReceiverAsset.Animation] Final layers");
            foreach (var l in userLayers) {
                UnityEngine.Debug.Log($"{l.Id} {l.CustomLayerID}");
            }

            Character.OverlappingAnimations = userLayers.ToArray();
            Character.BroadcastDataInput(nameof(Character.OverlappingAnimations));

            Context.Service.PromptMessage("SUCCESS", "Character Overlaying Animations has been synced.");
        }

        public class GamepadButtonAnimationData : StructuredData, ICollapsibleStructuredData {

            [DataInput]
            [PreviewGallery]
            [AutoCompleteResource("CharacterAnimation", null)]
            public string FingerHoverAnimation;

            [DataInput]
            [PreviewGallery]
            [AutoCompleteResource("CharacterAnimation", null)]
            public string FingerPressAnimation;

            [DataInput]
            [Label("Right side mask")]
            public bool MaskRight;

            [DataInput]
            public string PropLayerName;

            [Hidden]
            [DataInput]
            public ControllerType TargetControllerType;

            [Hidden]
            [DataInput]
            public int ButtonId;

            public string GetHeader() {
                string name;
                switch(TargetControllerType) {
                    case ControllerType.SwitchProController:
                    default:
                        name = $"{ButtonName} ({TargetControllerType})";
                        break;
                }
                if (ButtonId == 0) return "Invalid. Please Generate this list instead.";
                return name;
            }

            public string ButtonName {
                get {
                    switch(TargetControllerType) {
                        case ControllerType.SwitchProController:
                        default:
                            return Enum.GetName(typeof(SwitchProButton), ButtonId);
                    }
                }
            }

            public string HoverLayerId {
                get {
                    return $"🔥🎮 {ButtonName} Hover";
                }
            }

            public string PressLayerId {
                get {
                    return $"🔥🎮 {ButtonName} Press";
                }
            }
        }

        public class GamepadControlPadAnimationData : StructuredData, ICollapsibleStructuredData {

            [DataInput]
            [PreviewGallery]
            [AutoCompleteResource("CharacterAnimation", null)]
            public string FingerHoverAnimation;

            [DataInput]
            [PreviewGallery]
            [AutoCompleteResource("CharacterAnimation", null)]
            public string FingerPressAnimation;

            [DataInput]
            public string PropLayerName;

            [Hidden]
            [DataInput]
            public int ButtonId;

            public string GetHeader() {
                if (ButtonId == 0) return "Invalid. Please Generate this list instead.";
                return $"{ButtonName}";
            }

            public string ButtonName {
                get {
                    switch(ButtonId) {
                        case 1: return "↙️";
                        case 2: return "⬇️";
                        case 3: return "↘️";
                        case 4: return "⬅️";
                        case 6: return "➡️";
                        case 7: return "↖️";
                        case 8: return "⬆️";
                        case 9: return "↗️";
                    }

                    return Enum.GetName(typeof(ControlPadDirection), ButtonId);
                }
            }

            public string HoverLayerId {
                get {
                    return $"🔥🎮 {ButtonName} Hover";
                }
            }

            public string PressLayerId {
                get {
                    return $"🔥🎮 {ButtonName} Press";
                }
            }
        }
    }
}

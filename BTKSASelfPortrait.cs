using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using System.IO;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Networking;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using BTKSASelfPortrait.Config;
using BTKUILib;
using BTKUILib.UIObjects;
using BTKUILib.UIObjects.Components;
using BTKUILib.UIObjects.Objects;
using HarmonyLib;
using Semver;
using UnityEngine.UI;
using UnityEngine.Rendering.PostProcessing;
using Object = UnityEngine.Object;

namespace BTKSASelfPortrait
{
    public static class BuildInfo
    {
        public const string Name = "BTKSASelfPortrait";
        public const string Author = "DDAkebono#0001";
        public const string Company = "BTK-Development";
        public const string Version = "2.0.1";
        public const string DownloadLink = "https://github.com/ddakebono/BTKSASelfPortrait/releases";
    }

    public class BTKSASelfPortrait : MelonMod
    {
        public static BTKSASelfPortrait Instance;
        internal static MelonLogger.Instance Logger;

        internal static readonly List<BTKBaseConfig> BTKConfigs = new(); 

        private GameObject _cameraEye;
        private GameObject _hudContent;
        private GameObject _cameraGO;
        private GameObject _uiRtgo;
        private Camera _cameraComp;
        private RawImage _uiRawImage;

        private bool _hasInstantiatedPrefabs;
        private bool _lastVRCheck;
        private bool _ranInitalize;
        private AssetBundle _spBundle;
        private GameObject _cameraPrefab;
        private GameObject _uiPrefab;

        private readonly BTKFloatConfig _cameraDistance = new(nameof(BTKSASelfPortrait), "Camera Distance", "Sets how far the camera is from your view point", 0.5021816f, 0f, 10f, null, false);
        private readonly BTKFloatConfig _alphaPercentage = new(nameof(BTKSASelfPortrait), "Alpha Percentage", "Sets how transparent the Self Portrait is", .8f, 0f, 1f, null, false);
        private readonly BTKBoolConfig _flipDisplay = new(nameof(BTKSASelfPortrait), "Flip Display", "Flips the display of Self Portrait like a mirror", true, null, false);
        private readonly BTKBoolConfig _reflectOtherPlayers = new(nameof(BTKSASelfPortrait), "Reflect Other Players", "Sets if other players can be seen in Self Portrait", true, null, false);
        private readonly BTKBoolConfig _enableAtStart = new(nameof(BTKSASelfPortrait), "Enable At Start", "Sets Self Portrait to be enabled automatically", false, null, false);
        private readonly BTKFloatConfig _farClippingDistance = new(nameof(BTKSASelfPortrait), "Far Clipping Distance", "Sets how far objects will be visible in the camera", 5f, 0f, 30f, null, false);
        private readonly BTKFloatConfig _positionX = new(nameof(BTKSASelfPortrait), "Position X", "Sets the X position on your HUD for Self Portrait", 500.0f, -400f, 1000f, null, false);
        private readonly BTKFloatConfig _positionY = new(nameof(BTKSASelfPortrait), "Position Y", "Sets the Y position on your HUD for Self Portrait", -350.0f, -400f, 400f, null, false);
        private readonly BTKFloatConfig _scaleX = new(nameof(BTKSASelfPortrait), "Scale X", "Sets the X scale of Self Portrait on the HUD", .2f, 0f, 1f, null, false);
        private readonly BTKFloatConfig _scaleY = new(nameof(BTKSASelfPortrait), "Scale Y", "Sets the Y scale of Self Portrait on the HUD", 0.3f, 0f, 1f, null, false);

        public override void OnInitializeMelon()
        {
            Logger = LoggerInstance;
            
            Logger.Msg("BTK Standalone: Self Portrait - Starting Up");

            if (RegisteredMelons.Any(x => x.Info.Name.Equals("BTKCompanionLoader", StringComparison.OrdinalIgnoreCase)))
            {
                Logger.Msg("Hold on a sec! Looks like you've got BTKCompanion installed, this mod is built in and not needed!");
                Logger.Error("BTKSASelfPortrait has not started up! (BTKCompanion Running)");
                return;
            }
            
            if (!RegisteredMelons.Any(x => x.Info.Name.Equals("BTKUILib") && x.Info.SemanticVersion != null && x.Info.SemanticVersion.CompareTo(new SemVersion(0, 3)) >= 0))
            {
                Logger.Error("BTKUILib was not detected or it outdated! BTKCompanion cannot function without it!");
                Logger.Error("Please download an updated copy for BTKUILib!");
                return;
            }

            Instance = this;
            
            _positionX.OnConfigUpdated += o =>
            {
                if (_uiRtgo == null) return;
                var pos = _uiRtgo.transform.localPosition;
                pos.x = _positionX.FloatValue;
                _uiRtgo.transform.localPosition = pos;
            };

            _positionY.OnConfigUpdated += o =>
            {
                if (_uiRtgo == null) return;
                var pos = _uiRtgo.transform.localPosition;
                pos.y = _positionY.FloatValue;
                _uiRtgo.transform.localPosition = pos;
            };

            _scaleX.OnConfigUpdated += o =>
            {
                if (_uiRtgo == null) return;
                var scale = _uiRtgo.transform.localScale;
                scale.x = _scaleX.FloatValue;
                _uiRtgo.transform.localScale = scale;
            };

            _scaleY.OnConfigUpdated += o =>
            {
                if (_uiRtgo == null) return;
                var scale = _uiRtgo.transform.localScale;
                scale.y = _scaleY.FloatValue;
                _uiRtgo.transform.localScale = scale;
            };

            _alphaPercentage.OnConfigUpdated += o =>
            {
                if(_uiRawImage != null)
                    _uiRawImage.color = new Color(1, 1, 1, _alphaPercentage.FloatValue);
            };

            _cameraDistance.OnConfigUpdated += f =>
            {
                if (_cameraGO == null) return;
                _cameraGO.transform.localPosition = new Vector3(0, 0, _cameraDistance.FloatValue);
            };

            _farClippingDistance.OnConfigUpdated += f =>
            {
                if (_cameraComp == null) return;
                _cameraComp.farClipPlane = _farClippingDistance.FloatValue;
            };

            _flipDisplay.OnConfigUpdated += b =>
            {
                if(_uiRawImage == null) return;
                _uiRawImage.rectTransform.localEulerAngles = _flipDisplay.BoolValue ? new Vector3(0, -180f, 0) : new Vector3(0, 0, 0);
            };

            _reflectOtherPlayers.OnConfigUpdated += b =>
            {
                if (_cameraComp == null) return;
                
                if (_reflectOtherPlayers.BoolValue)
                    //Reflect other players
                    _cameraComp.cullingMask |= 1 << LayerMask.NameToLayer("PlayerNetwork");
                else
                    //Don't reflect other players
                    _cameraComp.cullingMask &= ~(1 << LayerMask.NameToLayer("PlayerNetwork"));
            };

            //Apply patches
            ApplyPatches(typeof(RichPresensePatch));

            LoadAssets();

            QuickMenuAPI.OnMenuRegenerate += LateStartup;
        }

        private void LateStartup(CVR_MenuManager unused)
        {
            if (_ranInitalize) return;

            _ranInitalize = true;
            
            RichPresensePatch.OnWorldJoin += ApplySPCameraAdjustments;
            
            GetHudElements();
            
            SetupUI();
            
            if(_enableAtStart.BoolValue)
                ToggleSelfPortrait(true);
        }

        public void ToggleSelfPortrait(bool state)
        {
            if (_lastVRCheck != MetaPort.Instance.isUsingVr)
            {
                //Time to move things!
                GetHudElements();
                
                if (_cameraGO != null && _uiRtgo != null)
                {
                    _cameraGO.transform.parent = _cameraEye.transform;
                    _uiRtgo.transform.parent = _hudContent.transform;
                }
            }
            
            if (state)
            {
                if(!_hasInstantiatedPrefabs)
                {
                    //Instantiate target prefabs
                    _cameraGO = Object.Instantiate(_cameraPrefab, _cameraEye.transform);
                    _uiRtgo = Object.Instantiate(_uiPrefab, _hudContent.transform);

                    _uiRawImage = _uiRtgo.GetComponentInChildren<RawImage>();
                    _cameraComp = _cameraGO.GetComponent<Camera>();

                    _hasInstantiatedPrefabs = true;
                }

                _cameraGO.SetActive(true);
                _uiRtgo.SetActive(true);

                ApplySPCameraAdjustments(null);
            }
            else
            {
                if (_hasInstantiatedPrefabs)
                {
                    _cameraGO.SetActive(false);
                    _uiRtgo.SetActive(false);
                }
            }
        }

        public void ApplySPCameraAdjustments(RichPresenceInstance_t richPresenceInstanceT)
        {
            if (_hasInstantiatedPrefabs)
            {
                //Ensure the camera is pointing correctly as set it's distance from the viewpoint
                _cameraGO.transform.localPosition = new Vector3(0, 0, _cameraDistance.FloatValue);
                _cameraGO.transform.localRotation = new Quaternion(0, 180, 0, 0);

                //Ensure the UI element is in the right spot
                _uiRtgo.transform.localPosition = new Vector3(_positionX.FloatValue, _positionY.FloatValue, 0);
                _uiRtgo.transform.localScale = new Vector3(_scaleX.FloatValue, _scaleY.FloatValue, _scaleX.FloatValue);

                //Set RawImage colour and rotation
                _uiRawImage.color = new Color(1, 1, 1, _alphaPercentage.FloatValue);
                _uiRawImage.rectTransform.localEulerAngles = _flipDisplay.BoolValue ? new Vector3(0, -180f, 0) : new Vector3(0, 0, 0);

                //Ensure camera colour doesn't get set to an alpha above 0, also set camera settings
                Color bgColour = new Color(0, 0, 0, 0);
                _cameraComp.backgroundColor = bgColour;
                _cameraComp.clearFlags = CameraClearFlags.SolidColor;
                _cameraComp.farClipPlane = _farClippingDistance.FloatValue;
                if (_reflectOtherPlayers.BoolValue)
                    //Reflect other players
                    _cameraComp.cullingMask |= 1 << LayerMask.NameToLayer("PlayerNetwork");
                else
                    //Don't reflect other players
                    _cameraComp.cullingMask &= ~(1 << LayerMask.NameToLayer("PlayerNetwork"));

                //Remove PostProcessLayers
                foreach (PostProcessLayer layer in _cameraGO.GetComponents<PostProcessLayer>())
                    Object.Destroy(layer);
            }
        }

        private void LoadAssets()
        {
            using (var assetStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BTKSASelfPortrait.spasset"))
            {
                if (assetStream != null)
                {
                    using var tempStream = new MemoryStream((int) assetStream.Length);
                    assetStream.CopyTo(tempStream);

                    _spBundle = AssetBundle.LoadFromMemory(tempStream.ToArray(), 0);
                    _spBundle.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                }
            }

            if (_spBundle != null)
            {
                _cameraPrefab = _spBundle.LoadAsset<GameObject>("SPCamera");
                _cameraPrefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                _uiPrefab = _spBundle.LoadAsset<GameObject>("RTOutput");
                _uiPrefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            }

            Logger.Msg("SelfPortrait assets have been loaded!");

        }
        
        private void ApplyPatches(Type type)
        {
            try
            {
                HarmonyInstance.PatchAll(type);
            }
            catch(Exception e)
            {
                Logger.Error($"Failed while patching {type.Name}!");
                Logger.Error(e);
            }
        }

        private void GetHudElements()
        {
            if (!MetaPort.Instance.isUsingVr)
            {
                _cameraEye = PlayerSetup.Instance.desktopCamera;
                _hudContent = PlayerSetup.Instance.desktopCamera.GetComponentInChildren<Canvas>().gameObject;
                _lastVRCheck = false;
            }
            else
            {
                _cameraEye = PlayerSetup.Instance.vrCamera;
                _hudContent = PlayerSetup.Instance.vrCamera.GetComponentInChildren<Canvas>().gameObject;
                _lastVRCheck = true;
            }
        }

        private void SetupUI()
        {
            QuickMenuAPI.PrepareIcon("BTKStandalone", "BTKIcon", Assembly.GetExecutingAssembly().GetManifestResourceStream("BTKSASelfPortrait.Images.BTKIcon.png"));
            QuickMenuAPI.PrepareIcon("BTKStandalone", "Settings", Assembly.GetExecutingAssembly().GetManifestResourceStream("BTKSASelfPortrait.Images.Settings.png"));

            var rootPage = new Page("BTKStandalone", "MainPage", true, "BTKIcon");
            rootPage.MenuTitle = "BTK Standalone Mods";
            rootPage.MenuSubtitle = "Toggle and configure your BTK Standalone mods here!";

            var functionToggles = rootPage.AddCategory("Self Portrait");

            var toggleSP = functionToggles.AddToggle("Self Portrait", "Toggles on self portrait", false);
            toggleSP.OnValueUpdated += b =>
            {
                ToggleSelfPortrait(b);
            };
            
            var settingsPage = functionToggles.AddPage("SP Settings", "Settings", "Change settings related to SelfPortrait", "BTKStandalone");

            var configCategories = new Dictionary<string, Category>();
            
            foreach (var config in BTKConfigs)
            {
                if (!configCategories.ContainsKey(config.Category)) 
                    configCategories.Add(config.Category, settingsPage.AddCategory(config.Category));

                var cat = configCategories[config.Category];

                switch (config.Type)
                {
                    case { } boolType when boolType == typeof(bool):
                        ToggleButton toggle = null;
                        var boolConfig = (BTKBoolConfig)config;
                        toggle = cat.AddToggle(config.Name, config.Description, boolConfig.BoolValue);
                        toggle.OnValueUpdated += b =>
                        {
                            if (!ConfigDialogs(config))
                                toggle.ToggleValue = boolConfig.BoolValue;

                            boolConfig.BoolValue = b;
                        };
                        break;
                    case {} floatType when floatType == typeof(float):
                        SliderFloat slider = null;
                        var floatConfig = (BTKFloatConfig)config;
                        slider = settingsPage.AddSlider(floatConfig.Name, floatConfig.Description, Convert.ToSingle(floatConfig.FloatValue), floatConfig.MinValue, floatConfig.MaxValue);
                        slider.OnValueUpdated += f =>
                        {
                            if (!ConfigDialogs(config))
                            {
                                slider.SetSliderValue(floatConfig.FloatValue);
                                return;
                            }

                            floatConfig.FloatValue = f;

                        };
                        break;
                }
            }
        }
        
        private bool ConfigDialogs(BTKBaseConfig config)
        {
            if (config.DialogMessage != null)
            {
                QuickMenuAPI.ShowNotice("Notice", config.DialogMessage);
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(RichPresence))]
    class RichPresensePatch
    {
        public static Action<RichPresenceInstance_t> OnWorldJoin;
        
        private static string _lastRichPresenseUpdate;
        private static FieldInfo _richPresenceLastMsgGetter = typeof(RichPresence).GetField("LastMsg", BindingFlags.Static | BindingFlags.NonPublic);
        
        [HarmonyPatch(nameof(RichPresence.DisplayMode), MethodType.Setter)]
        [HarmonyPrefix]
        static bool OnRichPresenseUpdated()
        {
            var rpInfo = GetRichPresenceInfo();

            if (rpInfo == null) return true;
            
            if (_lastRichPresenseUpdate == rpInfo.InstanceMeshId) return true;
            
            _lastRichPresenseUpdate = rpInfo.InstanceMeshId;

            try
            {
                OnWorldJoin?.Invoke(rpInfo);
            }
            catch (Exception e)
            {
                BTKSASelfPortrait.Logger.Error(e);
            }

            return true;
        }
        
        private static RichPresenceInstance_t GetRichPresenceInfo()
        {
            return _richPresenceLastMsgGetter.GetValue(null) as RichPresenceInstance_t;
        }
    }
}

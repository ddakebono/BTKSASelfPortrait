using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UIExpansionKit.API;
using UnhollowerRuntimeLib;
using UnityEngine;
using System.IO;
using HarmonyLib;
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
        public const string Version = "1.2.0";
        public const string DownloadLink = "https://github.com/ddakebono/BTKSASelfPortrait/releases";
    }

    public class BTKSASelfPortrait : MelonMod
    {
        public static BTKSASelfPortrait Instance;

        private GameObject _cameraEye;
        private GameObject _hudContent;
        private GameObject _cameraGO;
        private GameObject _uiRtgo;
        private Camera _cameraComp;
        private RawImage _uiRawImage;

        private bool _showSelfPortrait;
        private bool _hasInstantiatedPrefabs;
        private AssetBundle _spBundle;
        private GameObject _cameraPrefab;
        private GameObject _uiPrefab;

        private const string SettingsCategory = "BTKSASelfPortrait";
        private const string PrefsCameraDistance = "CameraDistance";
        private const string PrefsUIAlpha = "UIAlpha";
        private const string PrefsUIFlip = "UIFlip";
        private const string PrefsReflectOtherPlayers = "ReflectOthers";
        private const string PrefsFarClippingDist = "FarClippingDist";
        private const string PrefsPosX = "UIPosX";
        private const string PrefsPosY = "UIPosY";
        private const string PrefsScaleX = "UIScaleX";
        private const string PrefsScaleY = "UIScaleY";
        int _scenesLoaded = 0;
        
        //Local prefs
        private float _cameraDistance, _farClippingDist, _uiPosX, _uiPosY, _uiScaleX, _uiScaleY;
        private int _uiAlpha;
        private bool _uiFlip, _reflectOtherPlayers;

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (_scenesLoaded <= 2)
            {
                _scenesLoaded++;
                if (_scenesLoaded == 2)
                    UiManagerInit();
            }
        }

        public void UiManagerInit()
        {
            MelonLogger.Msg("BTK Standalone: Self Portrait - Starting Up");

            if (MelonHandler.Mods.Any(x => x.Info.Name.Equals("BTKCompanionLoader", StringComparison.OrdinalIgnoreCase)))
            {
                MelonLogger.Msg("Hold on a sec! Looks like you've got BTKCompanion installed, this mod is built in and not needed!");
                MelonLogger.Error("BTKSASelfPortrait has not started up! (BTKCompanion Running)");
                return;
            }

            Instance = this;

            MelonPreferences.CreateCategory(SettingsCategory, "BTKSA Self Portrait");
            MelonPreferences.CreateEntry(SettingsCategory, PrefsCameraDistance, 0.7f, "Camera Distance");
            MelonPreferences.CreateEntry(SettingsCategory, PrefsUIAlpha, 70, "UI Display Alpha Percentage");
            MelonPreferences.CreateEntry(SettingsCategory, PrefsUIFlip, true, "Flip Display (Matches mirrors)");
            MelonPreferences.CreateEntry(SettingsCategory, PrefsReflectOtherPlayers, false, "Reflect Other Players");
            MelonPreferences.CreateEntry(SettingsCategory, PrefsFarClippingDist, 5f, "Camera Cutoff Distance");
            MelonPreferences.CreateEntry(SettingsCategory, PrefsPosX, 300f, "UI Position X");
            MelonPreferences.CreateEntry(SettingsCategory, PrefsPosY, -250f, "UI Position Y");
            MelonPreferences.CreateEntry(SettingsCategory, PrefsScaleX, 0.4f, "UI Scale X");
            MelonPreferences.CreateEntry(SettingsCategory, PrefsScaleY, 0.47f, "UI Scale Y");

            ExpansionKitApi.GetExpandedMenu(ExpandedMenu.QuickMenu).AddSimpleButton("Toggle Self Portrait", ToggleSelfPortrait);
            
            //Apply patches
            applyPatches(typeof(FadePatches));

            LoadAssets();

            _cameraEye = GameObject.Find("Camera (eye)");
            _hudContent = GameObject.Find("/UserInterface/UnscaledUI/HudContent");
        }
        
        private void applyPatches(Type type)
        {
            try
            {
                HarmonyLib.Harmony.CreateAndPatchAll(type, "BTKHarmonyInstance");
            }
            catch(Exception e)
            {
                MelonLogger.Error($"Failed while patching {type.Name}!");
                MelonLogger.Error(e);
            }
        }

        public override void OnPreferencesSaved()
        {
            _cameraDistance = MelonPreferences.GetEntryValue<float>(SettingsCategory, PrefsCameraDistance);
            _farClippingDist = MelonPreferences.GetEntryValue<float>(SettingsCategory, PrefsFarClippingDist);
            _uiPosX = MelonPreferences.GetEntryValue<float>(SettingsCategory, PrefsPosX);
            _uiPosY = MelonPreferences.GetEntryValue<float>(SettingsCategory, PrefsPosY);
            _uiScaleX = MelonPreferences.GetEntryValue<float>(SettingsCategory, PrefsScaleX);
            _uiScaleY = MelonPreferences.GetEntryValue<float>(SettingsCategory, PrefsScaleY);
            _uiAlpha = MelonPreferences.GetEntryValue<int>(SettingsCategory, PrefsUIAlpha);
            _uiFlip = MelonPreferences.GetEntryValue<bool>(SettingsCategory, PrefsUIFlip);
            _reflectOtherPlayers = MelonPreferences.GetEntryValue<bool>(SettingsCategory, PrefsReflectOtherPlayers);
            
            ApplySpCameraAdjustments();
        }

        public void ToggleSelfPortrait()
        {
            if (!_showSelfPortrait)
            {
                if(!_hasInstantiatedPrefabs)
                {
                    //Instantiate target prefabs
                    _cameraGO = Object.Instantiate(_cameraPrefab, _cameraEye.transform);
                    _uiRtgo = Object.Instantiate(_uiPrefab, _hudContent.transform);

                    _uiRawImage = _uiRtgo.GetComponent<RawImage>();
                    _cameraComp = _cameraGO.GetComponent<Camera>();

                    _hasInstantiatedPrefabs = true;
                }

                _cameraGO.SetActive(true);
                _uiRtgo.SetActive(true);

                ApplySpCameraAdjustments();
                _showSelfPortrait = true;
            }
            else
            {
                if (_hasInstantiatedPrefabs)
                {
                    _cameraGO.SetActive(false);
                    _uiRtgo.SetActive(false);
                }

                _showSelfPortrait = false;
            }
        }

        public void ApplySpCameraAdjustments()
        {
            if (_hasInstantiatedPrefabs)
            {
                _cameraGO.transform.localPosition = new Vector3(0, 0, _cameraDistance);
                _cameraGO.transform.localRotation = new Quaternion(0, 180, 0, 0);

                _uiRtgo.transform.localPosition = new Vector3(_uiPosX, _uiPosY, 0);
                _uiRtgo.transform.localScale = new Vector3(_uiScaleX, _uiScaleY, _uiScaleX);

                _uiRawImage.color = new Color(1, 1, 1, _uiAlpha / 100f);
                if (_uiFlip)
                    _uiRawImage.rectTransform.localEulerAngles = new Vector3(0, -180f, 0);
                else
                    _uiRawImage.rectTransform.localEulerAngles = new Vector3(0, 0, 0);

                //Ensure camera colour doesn't get set to an alpha above 0
                Color bgColour = new Color(0, 0, 0, 0);
                _cameraComp.backgroundColor = bgColour;
                _cameraComp.clearFlags = CameraClearFlags.SolidColor;
                _cameraComp.farClipPlane = _farClippingDist;
                if (_reflectOtherPlayers)
                    //Reflect other players
                    _cameraComp.cullingMask |= 1 << LayerMask.NameToLayer("Player");
                else
                    //Don't reflect other players
                    _cameraComp.cullingMask &= ~(1 << LayerMask.NameToLayer("Player"));

                //Remove PostProcessLayers
                foreach (PostProcessLayer layer in _cameraGO.GetComponents<PostProcessLayer>())
                    Object.Destroy(layer);

                Log("Applied Adjustments", true);
            }
        }

        private void LoadAssets()
        {
            using (var assetStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BTKSASelfPortrait.spasset"))
            {
                Log("Loaded Embedded resource", true);
                using (var tempStream = new MemoryStream((int)assetStream.Length))
                {
                    assetStream.CopyTo(tempStream);

                    _spBundle = AssetBundle.LoadFromMemory_Internal(tempStream.ToArray(), 0);
                    _spBundle.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                }
            }

            if (_spBundle != null)
            {
                _cameraPrefab = _spBundle.LoadAsset_Internal("SPCamera", Il2CppType.Of<GameObject>()).Cast<GameObject>();
                _cameraPrefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                _uiPrefab = _spBundle.LoadAsset_Internal("RTOutput", Il2CppType.Of<GameObject>()).Cast<GameObject>();
                _uiPrefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            }

            Log("Loaded Assets Successfully!", true);

        }

        public static void Log(string log, bool dbg = false)
        {
            if (!MelonDebug.IsEnabled() && dbg)
                return;

            MelonLogger.Msg(log);
        }
    }
    
    [HarmonyPatch]
    class FadePatches 
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            return typeof(VRCUiBackgroundFade).GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(x => x.Name.Contains("Method_Public_Void_Single_Action") && !x.Name.Contains("PDM")).Cast<MethodBase>();
        }

        static void Postfix()
        {
            //Make sure all settings are applied on fade
            BTKSASelfPortrait.Instance.ApplySpCameraAdjustments();
        }
    }
}

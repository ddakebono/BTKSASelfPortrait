using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using UIExpansionKit.API;
using UnhollowerRuntimeLib;
using UnityEngine;
using System.IO;
using Harmony;
using VRC.SDKBase;
using VRC;
using UnityEngine.UI;
using UnityEngine.Rendering.PostProcessing;

namespace BTKSASelfPortrait
{
    public static class BuildInfo
    {
        public const string Name = "BTKSASelfPortrait";
        public const string Author = "DDAkebono#0001";
        public const string Company = "BTK-Development";
        public const string Version = "1.1.0";
        public const string DownloadLink = "https://github.com/ddakebono/BTKSASelfPortrait/releases";
    }

    public class BTKSASelfPortrait : MelonMod
    {
        public static BTKSASelfPortrait instance;

        public HarmonyInstance harmony;

        private GameObject cameraEye;
        private GameObject hudContent;
        private GameObject cameraGO;
        private GameObject uiRTGO;
        private Camera cameraComp;
        private RawImage uiRawImage;

        private bool showSelfPortrait;
        private bool hasInstantiatedPrefabs;
        private AssetBundle spBundle;
        private GameObject cameraPrefab;
        private GameObject uiPrefab;

        private string settingsCategory = "BTKSASelfPortrait";
        private string prefsCameraDistance = "CameraDistance";
        private string prefsUIAlpha = "UIAlpha";
        private string prefsUIFlip = "UIFlip";
        private string prefsReflectOtherPlayers = "ReflectOthers";
        private string prefsFarClippingDist = "FarClippingDist";


        public override void VRChat_OnUiManagerInit()
        {
            MelonLogger.Log("BTK Standalone: Self Portrait - Starting Up");

            if (MelonHandler.Mods.Any(x => x.Info.Name.Equals("BTKCompanionLoader", StringComparison.OrdinalIgnoreCase)))
            {
                MelonLogger.Log("Hold on a sec! Looks like you've got BTKCompanion installed, this mod is built in and not needed!");
                MelonLogger.LogError("BTKSASelfPortrait has not started up! (BTKCompanion Running)");
                return;
            }

            instance = this;

            harmony = HarmonyInstance.Create("BTKStandaloneSP");

            MelonPrefs.RegisterCategory(settingsCategory, "BTKSA Self Portrait");
            MelonPrefs.RegisterFloat(settingsCategory, prefsCameraDistance, 0.7f, "Camera Distance");
            MelonPrefs.RegisterInt(settingsCategory, prefsUIAlpha, 70, "UI Display Alpha Percentage");
            MelonPrefs.RegisterBool(settingsCategory, prefsUIFlip, true, "Flip Display (Matches mirrors)");
            MelonPrefs.RegisterBool(settingsCategory, prefsReflectOtherPlayers, false, "Reflect Other Players");
            MelonPrefs.RegisterFloat(settingsCategory, prefsFarClippingDist, 5f, "Camera Cutoff Distance");

            ExpansionKitApi.RegisterSimpleMenuButton(ExpandedMenu.QuickMenu, "Toggle Self Portrait", toggleSelfPortrait);

            //Using FadeTo hook to determine when world is pretty much loaded
            harmony.Patch(typeof(VRCUiBackgroundFade).GetMethod("Method_Public_Void_Single_Action_0", BindingFlags.Instance | BindingFlags.Public), null, new HarmonyMethod(typeof(BTKSASelfPortrait).GetMethod("OnFade", BindingFlags.Public | BindingFlags.Static)));

            loadAssets();

            cameraEye = GameObject.Find("Camera (eye)");
            hudContent = GameObject.Find("/UserInterface/UnscaledUI/HudContent");
        }

        public override void OnModSettingsApplied()
        {
            applySPCameraAdjustments();
        }

        public static void OnFade()
        {
            //Make sure all settings are applied on fade
            BTKSASelfPortrait.instance.applySPCameraAdjustments();
        }

        public void toggleSelfPortrait()
        {
            if (!showSelfPortrait)
            {
                if(!hasInstantiatedPrefabs)
                {
                    //Instantiate target prefabs
                    cameraGO = GameObject.Instantiate(cameraPrefab, cameraEye.transform);
                    uiRTGO = GameObject.Instantiate(uiPrefab, hudContent.transform);

                    uiRawImage = uiRTGO.GetComponent<RawImage>();
                    cameraComp = cameraGO.GetComponent<Camera>();

                    hasInstantiatedPrefabs = true;
                }

                cameraGO.SetActive(true);
                uiRTGO.SetActive(true);

                applySPCameraAdjustments();
                showSelfPortrait = true;
            }
            else
            {
                if (hasInstantiatedPrefabs)
                {
                    cameraGO.SetActive(false);
                    uiRTGO.SetActive(false);
                }

                showSelfPortrait = false;
            }
        }

        public void applySPCameraAdjustments()
        {
            if (hasInstantiatedPrefabs)
            {
                cameraGO.transform.localPosition = new Vector3(0, 0, MelonPrefs.GetFloat(settingsCategory, prefsCameraDistance));
                cameraGO.transform.localRotation = new Quaternion(0, 180, 0, 0);

                uiRTGO.transform.localPosition = new Vector3(300, -250, 0);
                uiRTGO.transform.localScale = new Vector3(0.4f, 0.47f, 0.4f);

                uiRawImage.color = new Color(1, 1, 1, MelonPrefs.GetInt(settingsCategory, prefsUIAlpha) / 100f);
                if (MelonPrefs.GetBool(settingsCategory, prefsUIFlip))
                    uiRawImage.rectTransform.localEulerAngles = new Vector3(0, -180f, 0);
                else
                    uiRawImage.rectTransform.localEulerAngles = new Vector3(0, 0, 0);

                //Ensure camera colour doesn't get set to an alpha above 0
                Color bgColour = new Color(0, 0, 0, 0);
                cameraComp.backgroundColor = bgColour;
                cameraComp.clearFlags = CameraClearFlags.SolidColor;
                cameraComp.farClipPlane = MelonPrefs.GetFloat(settingsCategory, prefsFarClippingDist);
                if (MelonPrefs.GetBool(settingsCategory, prefsReflectOtherPlayers))
                    //Reflect other players
                    cameraComp.cullingMask |= 1 << LayerMask.NameToLayer("Player");
                else
                    //Don't reflect other players
                    cameraComp.cullingMask &= ~(1 << LayerMask.NameToLayer("Player"));

                //Remove PostProcessLayers
                foreach (PostProcessLayer layer in cameraGO.GetComponents<PostProcessLayer>())
                    GameObject.Destroy(layer);

                Log("Applied Adjustments", true);
            }
        }

        private void loadAssets()
        {
            using (var assetStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BTKSASelfPortrait.spasset"))
            {
                Log("Loaded Embedded resource", true);
                using (var tempStream = new MemoryStream((int)assetStream.Length))
                {
                    assetStream.CopyTo(tempStream);

                    spBundle = AssetBundle.LoadFromMemory_Internal(tempStream.ToArray(), 0);
                    spBundle.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                }
            }

            if (spBundle != null)
            {
                cameraPrefab = spBundle.LoadAsset_Internal("SPCamera", Il2CppType.Of<GameObject>()).Cast<GameObject>();
                cameraPrefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                uiPrefab = spBundle.LoadAsset_Internal("RTOutput", Il2CppType.Of<GameObject>()).Cast<GameObject>();
                uiPrefab.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            }

            Log("Loaded Assets Successfully!", true);

        }

        public static void Log(string log, bool dbg = false)
        {
            if (!Imports.IsDebugMode() && dbg)
                return;

            MelonLogger.Log(log);
        }
    }
}

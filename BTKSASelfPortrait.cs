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
        public const string Version = "1.0.2";
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


        public override void VRChat_OnUiManagerInit()
        {
            MelonLogger.Log("BTK Standalone: Self Portrait - Starting Up");

            if (Directory.Exists("BTKCompanion"))
            {
                MelonLogger.Log("Woah, hold on a sec, it seems you might be running BTKCompanion, if this is true BTKSASelfPortrait is built into that, and you should not be using this!");
                MelonLogger.Log("If you are not currently using BTKCompanion please remove the BTKCompanion folder from your VRChat installation!");
                MelonLogger.LogError("BTKSASelfPortrait has not started up! (BTKCompanion Exists)");
                return;
            }

            instance = this;

            harmony = HarmonyInstance.Create("BTKStandaloneSP");

            

            MelonPrefs.RegisterCategory(settingsCategory, "BTKSA Self Portrait");
            MelonPrefs.RegisterFloat(settingsCategory, prefsCameraDistance, 0.7f, "Camera Distance");
            MelonPrefs.RegisterInt(settingsCategory, prefsUIAlpha, 70, "UI Display Alpha Percentage");

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
                cameraComp.clearFlags = CameraClearFlags.SolidColor;

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

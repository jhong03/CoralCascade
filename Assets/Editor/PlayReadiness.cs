#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CoralCascade.EditorTools
{
    /// <summary>
    /// One-click application + audit of the Google Play release configuration.
    ///
    /// WHY THIS EXISTS: most player settings are plain fields in ProjectSettings.asset and
    /// can be edited by hand, but ICON assignment is a nested serialized structure keyed by
    /// platform and icon KIND — hand-writing that YAML is how you silently end up with an
    /// app that has no launcher icon. Going through PlayerSettings.SetIcons lets Unity
    /// itself write the structure. It is idempotent, so it doubles as a pre-release check:
    /// run "Verify" any time and it reports what is still missing.
    ///
    /// Menu: Coral Cascade ▸ Play Store ▸ …
    /// </summary>
    public static class PlayReadiness
    {
        private const string IconFull       = "Assets/Art/Icon/icon_full.png";
        private const string IconForeground = "Assets/Art/Icon/icon_foreground.png";
        private const string IconBackground = "Assets/Art/Icon/icon_background.png";

        private const string AppId = "com.jhong03.coralcascade";
        // API 35 has been the floor for new apps since 2025-08-31; API 36 becomes mandatory
        // for new apps AND updates on 2026-08-31. Shipping 36 now avoids a forced redo.
        private const int TargetSdk = 36;
        private const int MinSdk = 25;         // no Play-enforced floor; check the ads SDK's own

        [MenuItem("Coral Cascade/Play Store/Apply Release Settings")]
        public static void Apply()
        {
            var log = new StringBuilder("Coral Cascade — applying release settings\n");
            if (!AndroidModuleInstalled)
                log.AppendLine("  WARNING           Android Build Support is NOT installed — the " +
                               "settings below will save, but icons cannot be assigned and no " +
                               "APK/AAB can be built until you add the module in Unity Hub.");

            PlayerSettings.companyName = "jhong03";
            PlayerSettings.productName = "Coral Cascade";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
            log.AppendLine("  app id            " + AppId);

            // 64-bit only + IL2CPP: Play requires a 64-bit binary, and Mono cannot produce
            // one on Android — these two settings must move together.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            log.AppendLine("  scripting         IL2CPP / ARM64");

            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)MinSdk;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)TargetSdk;
            EditorUserBuildSettings.buildAppBundle = true; // Play requires AAB for new apps
            log.AppendLine($"  sdk               min {MinSdk} / target {TargetSdk}, AAB on");

            // Portrait only — matches the 2026-07-16 decision and the whole layout.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Render under the cutout rather than letterboxing beside it; the UI now insets
            // itself with Screen.safeArea, so this is the correct pairing.
            PlayerSettings.Android.renderOutsideSafeArea = true;

            ApplyIcons(log);

            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
            Verify();
        }

        private static void ApplyIcons(StringBuilder log)
        {
            var full = AssetDatabase.LoadAssetAtPath<Texture2D>(IconFull);
            var fore = AssetDatabase.LoadAssetAtPath<Texture2D>(IconForeground);
            var back = AssetDatabase.LoadAssetAtPath<Texture2D>(IconBackground);
            if (full == null || fore == null || back == null)
            {
                log.AppendLine("  ICONS MISSING     expected " + IconFull + " (+ _foreground/_background)");
                return;
            }

            // Enumerate the kinds Unity itself reports for Android (Legacy / Round /
            // Adaptive) rather than naming AndroidPlatformIconKind: that type ships in the
            // Android platform extension, so referencing it fails to compile whenever the
            // Android module isn't installed. maxLayerCount tells us which kinds are the
            // two-layer adaptive ones without hardcoding any of it.
            int kindCount = 0;
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                for (int i = 0; i < icons.Length; i++)
                {
                    icons[i].SetTextures(icons[i].maxLayerCount >= 2
                        ? new[] { fore, back }   // adaptive: foreground over background
                        : new[] { full });
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
                kindCount++;
            }
            if (kindCount == 0)
                log.AppendLine("  ICONS NOT SET     Android reports no icon kinds — is Android " +
                               "Build Support installed? (Unity Hub ▸ Add modules)");
            else
                log.AppendLine($"  icons             {kindCount} Android icon kinds assigned");
        }

        /// <summary>
        /// True when this editor can actually build for Android. Without the module the
        /// Android player settings still SERIALISE fine (so the inspector looks configured)
        /// but icons cannot be assigned and no APK/AAB can be produced — which surfaces as
        /// a baffling "no launcher icon" instead of the real cause. Check it first.
        /// </summary>
        private static bool AndroidModuleInstalled =>
            BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);

        [MenuItem("Coral Cascade/Play Store/Verify Release Settings")]
        public static void Verify()
        {
            var problems = new List<string>();

            if (!AndroidModuleInstalled)
            {
                // Everything else is moot until this is fixed, so report it alone.
                Debug.LogError(
                    "Coral Cascade — ANDROID BUILD SUPPORT IS NOT INSTALLED. No APK/AAB can be " +
                    "built and launcher icons cannot be assigned.\n" +
                    "  Fix: Unity Hub ▸ Installs ▸ 6000.5.3f1 ▸ gear ▸ Add modules ▸ " +
                    "Android Build Support (tick OpenJDK and Android SDK & NDK Tools too).\n" +
                    "  Then re-run Coral Cascade ▸ Play Store ▸ Apply Release Settings.");
                return;
            }

            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != AppId)
                problems.Add("application identifier is not " + AppId);
            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android).Contains("DefaultCompany"))
                problems.Add("application identifier still says DefaultCompany — this is PERMANENT once published");
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
                problems.Add("Android scripting backend is not IL2CPP (required for ARM64)");
            if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
                problems.Add("ARM64 is not in the target architectures — Play requires a 64-bit binary");
            if ((int)PlayerSettings.Android.targetSdkVersion != 0 &&
                (int)PlayerSettings.Android.targetSdkVersion < TargetSdk)
                problems.Add($"target SDK is below {TargetSdk}");
            if (!EditorUserBuildSettings.buildAppBundle)
                problems.Add("Build App Bundle is off — Play requires an AAB for new apps");
            bool anyIcon = false;
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
                foreach (var icon in PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind))
                    foreach (var tex in icon.GetTextures())
                        if (tex != null) anyIcon = true;
            if (!anyIcon) problems.Add("no launcher icon assigned");

            // The blank-app trap: the build must actually contain the game scene.
            bool gameSceneEnabled = false;
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled && s.path.EndsWith("Phase1Prototype.unity")) gameSceneEnabled = true;
            if (!gameSceneEnabled)
                problems.Add("Phase1Prototype.unity is not an enabled scene in Build Settings — the build would launch empty");

            if (string.IsNullOrEmpty(PlayerSettings.Android.keystoreName))
                problems.Add("no keystore configured (needed for a release AAB; see the publishing checklist)");

            if (problems.Count == 0)
            {
                Debug.Log("Coral Cascade — release settings OK.");
                return;
            }
            // The Unity console list shows only the FIRST LINE of a message, so put the
            // summary there — "NOT ready:" on its own tells you nothing without clicking.
            var headline = new StringBuilder("Coral Cascade — ")
                .Append(problems.Count).Append(problems.Count == 1 ? " problem: " : " problems: ");
            for (int i = 0; i < problems.Count; i++)
            {
                if (i > 0) headline.Append("; ");
                headline.Append(Summarise(problems[i]));
            }
            var sb = new StringBuilder(headline.ToString()).AppendLine();
            foreach (var p in problems) sb.AppendLine("  • " + p);
            Debug.LogWarning(sb.ToString());
        }

        /// <summary>First few words of a problem, for the one-line console summary.</summary>
        private static string Summarise(string problem)
        {
            int cut = problem.IndexOf(" — ");
            if (cut < 0) cut = problem.IndexOf(" (");
            string s = cut > 0 ? problem.Substring(0, cut) : problem;
            return s.Length <= 46 ? s : s.Substring(0, 44) + "…";
        }
    }
}
#endif

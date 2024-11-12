#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnityWebSocket
{
    // This class is used to force enable the WebGL implementation
    // You can enable it by menu "Tools/UnityWebSocket/FORCE_WEBGL_IMPL_ENABLE"
    // If you enable it, the WebGL implementation will be used even if the platform is not WebGL
    // You can write and compile the code in the editor, but it will not work in the build
    // And it will show an error message when you build the project
    public class DefineSymbol_FORCE_WEBGL_IMPL_ENABLE : IPreprocessBuildWithReport
    {
        public const string SYMBOL_NAME = "FORCE_WEBGL_IMPL_ENABLE";

        public const string MENU_TITLE = "Tools/UnityWebSocket/Debug/" + SYMBOL_NAME;

        public int callbackOrder => -1;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (SYMBOL_IS_ENABLE)
            {
                SYMBOL_IS_ENABLE = false;
                throw new BuildFailedException(
                    $"The define symbol '{SYMBOL_NAME}' is enabled, It is only used for editor. "
                    + "You don't need to manually turn on this setting, it will automatically switch when switching platforms.");
            }
        }


        [MenuItem(MENU_TITLE, false, 1024)]
        static void DefineSymbolCheckMenu()
        {
            SYMBOL_IS_ENABLE = !SYMBOL_IS_ENABLE;
            Menu.SetChecked(MENU_TITLE, SYMBOL_IS_ENABLE);
        }

        // The menu won't be gray out, we use this validate method for update check state
        [MenuItem(MENU_TITLE, true)]
        static bool DefineSymbolMenuValidate()
        {
            Menu.SetChecked(MENU_TITLE, SYMBOL_IS_ENABLE);
            return true;
        }

        static bool SYMBOL_IS_ENABLE
        {
            get
            {
                var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
                var sdsList = DefineSymbols_GetStat(buildTargetGroup);
                return sdsList.Contains(SYMBOL_NAME);
            }

            set
            {
                var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
                var sdsList = DefineSymbols_GetStat(buildTargetGroup);
                if (value)
                {
                    if (!sdsList.Contains(SYMBOL_NAME))
                        sdsList.Add(SYMBOL_NAME);
                }
                else
                {
                    if (sdsList.Contains(SYMBOL_NAME))
                        sdsList.Remove(SYMBOL_NAME);
                }
                DefineSymbols_ApplyChanges(buildTargetGroup, sdsList);
            }
        }

        private static List<string> DefineSymbols_GetStat(BuildTargetGroup buildTargetGroup)
        {
            var sds = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);

            List<string> sdsList;
            if (string.IsNullOrWhiteSpace(sds))
                sdsList = new List<string>();
            else
                sdsList = new List<string>(sds.Split(';'));

            return sdsList;
        }

        private static void DefineSymbols_ApplyChanges(BuildTargetGroup buildTargetGroup, List<string> sdsList)
        {
            if (sdsList.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(sdsList[0]);
                for (int i = 1; i < sdsList.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(sdsList[i]))
                    {
                        sb.Append(';');
                        sb.Append(sdsList[i]);
                    }
                }
                PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, sb.ToString());
            }
            else
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, "");
            }
        }
    }
}
#endif
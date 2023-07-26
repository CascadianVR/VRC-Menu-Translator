#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;


// TODO
// List menu controls and select which to translate
// From -> To language options
// More error checking + limit warning
// 

namespace CasTools
{
    public class VRCMenuTranslator : EditorWindow
    {
        private GameObject currentAvatar;
        private VRCAvatarDescriptor currentAvatarDescriptor;
        
        private Vector2 scrollPos;
        private static int totalNumberOfControls = 0;
        private static int currentControlNumber = 0;

        //States
        private bool isTranslatingall = false;

        private readonly string[] Languages = new string[]
        {
            "English",
            "Japanese",
            "Arabic",
            "Spanish",
            "Chinese (Simplified)",
            "Russian",
            "Portuguese (Brazil)",
            "French",
            "German",
            "Korean",
            "Italian",
            "Hindi",
            "Turkish",
            "Vietnamese",
            "Dutch",
            "Ukrainian",
            "Indonesian",
            "Thai",
            "Malay",
            "Afrikaans",
        };

        enum LanguageIndex
        {
            en = 0, // English
            ja = 1, // Japanese
            ar = 2, // Arabic
            es = 3, // Spanish
            zh_CN = 4, // Chinese (Simplified)
            ru = 5, // Russian
            pt_BR = 6, // Portuguese (Brazil)
            fr = 7, // French
            de = 8, // German
            ko = 9, // Korean
            it = 10, // Italian
            hi = 11, // Hindi
            tr = 12, // Turkish
            vi = 13, // Vietnamese
            nl = 14, // Dutch
            uk = 15, // Ukrainian
            id = 16, // Indonesian
            th = 17, // Thai
            ms = 18, // Malay
            af = 19, // Afrikaans
        }

        private int fromLanguageIndex = 0;
        private int toLanguageIndex = 0;

        private void OnValidate()
        {
            totalNumberOfControls = 0;
            currentControlNumber = 0;
        }

        [MenuItem("Cascadian/VRCMenuTranslator")]
        private static void Init()
        {
            // Get existing open window or if none, make a new one:
            VRCMenuTranslator window = (VRCMenuTranslator)GetWindow(typeof(VRCMenuTranslator));
            window.Show();
            window.minSize = new Vector2(450, 650);

            totalNumberOfControls = 0;
            currentControlNumber = 0;
        }

        private async void OnGUI()
        {
            GUILayout.BeginVertical("Box");
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Avatar: ", new GUIStyle("BoldLabel"));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            currentAvatar = (GameObject)EditorGUILayout.ObjectField(currentAvatar, typeof(GameObject), true, GUILayout.Height(30));

            // Button to switch between JP and English
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            fromLanguageIndex = EditorGUILayout.Popup(fromLanguageIndex, Languages, GUILayout.Width(100));
            GUILayout.Label("⟶", new GUIStyle("BoldLabel"));
            toLanguageIndex = EditorGUILayout.Popup(toLanguageIndex, Languages, GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            if (currentAvatar == null) return;

            currentAvatarDescriptor = currentAvatar.GetComponent<VRCAvatarDescriptor>();
            if (currentAvatarDescriptor == null)
            {
                Debug.LogError("No avatar descriptor found");
                return;
            }

            List<VRCExpressionsMenu> expressionMenus = GetVRCMenus(currentAvatarDescriptor.expressionsMenu);

            if (isTranslatingall)
            {
                var oldColor = GUI.contentColor;
                EditorGUI.ProgressBar(new Rect(5f, 82, 440f, 40f), (float)currentControlNumber / (float)totalNumberOfControls, "Translating...");
                GUILayout.Space(42);
                GUI.contentColor = oldColor;
            }
            else if (GUILayout.Button("Translate All", GUILayout.Height(40)))
            {
                isTranslatingall = true;
                GetNumberOfControls(expressionMenus);

                currentAvatarDescriptor = currentAvatar.GetComponent<VRCAvatarDescriptor>();
                if (currentAvatarDescriptor == null)
                {
                    Debug.LogError("No avatar descriptor found");
                    return;
                }

                foreach (var menu in expressionMenus)
                {
                    Debug.Log("Menu: " + menu.name);
                    foreach (var control in menu.controls)
                    {
                        currentControlNumber += 1;
                        Debug.Log(currentControlNumber + " | " + totalNumberOfControls);
                        control.name = CapitalizeEveryWord(await Task.Run(() => TranslateTextGoogle(control.name)));
                        Repaint();
                    }
                }

                isTranslatingall = false;
                return;
            }

            // Single control listing and translation
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
            foreach (var menu in expressionMenus)
            {
                GUILayout.BeginVertical("Box");

                GUILayout.Label(menu.name);

                foreach (var control in menu.controls)
                {
                    GUILayout.BeginHorizontal("Button");
                    GUILayout.Label(control.name);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Translate", GUILayout.Height(30)))
                    {
                        control.name = CapitalizeEveryWord(await Task.Run(() => TranslateTextGoogle(control.name)));
                        return;
                    }

                    GUILayout.EndHorizontal();
                }

                GUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }

        private void GetNumberOfControls(List<VRCExpressionsMenu> expressionMenus)
        {
            foreach (var menu in expressionMenus)
            {
                totalNumberOfControls += menu.controls.Count;
            }
        }

        private Task<string> TranslateTextGoogle(string input)
        {

            // Set the language from/to in the url (or pass it into this function)
            string url = String.Format("https://translate.googleapis.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&q={2}",
                (LanguageIndex)fromLanguageIndex,
                (LanguageIndex)toLanguageIndex,
                input
            );

            HttpClient httpClient = new HttpClient();
            string result = httpClient.GetStringAsync(url).Result;

            // Get all json data
            var jsonData = JsonConvert.DeserializeObject<List<dynamic>>(result);

            // Extract just the first array element (This is the only data we are interested in)
            var translationItems = jsonData[0];

            // Translation Data
            string translation = "";

            // Loop through the collection extracting the translated objects
            foreach (object item in translationItems)
            {
                // Convert the item array to IEnumerable
                IEnumerable translationLineObject = item as IEnumerable;

                // Convert the IEnumerable translationLineObject to a IEnumerator
                IEnumerator translationLineString = translationLineObject.GetEnumerator();

                // Get first object in IEnumerator
                translationLineString.MoveNext();

                // Save its value (translated text)
                translation += string.Format(" {0}", Convert.ToString(translationLineString.Current));
            }

            // Remove first blank character
            if (translation.Length > 1)
            {
                translation = translation.Substring(1);
            }

            ;

            Debug.Log("____Control: " + input);
            Debug.Log("____Translation: " + translation);

            return Task.FromResult(translation);
        }

        private List<VRCExpressionsMenu> GetVRCMenus(VRCExpressionsMenu mainMenu)
        {
            List<VRCExpressionsMenu> menus = new List<VRCExpressionsMenu>();
            menus.Add(mainMenu);
            CheckMenu(mainMenu, ref menus);
            return menus;
        }

        static void CheckMenu(VRCExpressionsMenu currentMenu, ref List<VRCExpressionsMenu> menus)
        {
            for (int i = 0; i < currentMenu.controls.Count; i++)
            {
                if (currentMenu.controls[i].type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                {
                    menus.Add(currentMenu.controls[i].subMenu);
                    CheckMenu(currentMenu.controls[i].subMenu, ref menus);
                }
            }
        }

        string CapitalizeEveryWord(string input)
        {
            // Check for null or empty input
            if (string.IsNullOrEmpty(input) || toLanguageIndex != 0)
                return input;

            char[] charArray = input.ToCharArray();
            bool foundWordStart = false;

            // Capitalize the first character if it's a letter
            if (char.IsLetter(charArray[0]))
            {
                charArray[0] = char.ToUpper(charArray[0]);
                foundWordStart = true;
            }

            // Loop through the characters
            for (int i = 1; i < charArray.Length; i++)
            {
                if (char.IsWhiteSpace(charArray[i]) || charArray[i] == '.')
                {
                    foundWordStart = false;
                }
                else if (char.IsLetter(charArray[i]) && !foundWordStart)
                {
                    charArray[i] = char.ToUpper(charArray[i]);
                    foundWordStart = true;
                }
                else
                {
                    charArray[i] = char.ToLower(charArray[i]);
                }
            }

            return new string(charArray);
        }
    }
}
#endif
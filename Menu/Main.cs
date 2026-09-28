using BepInEx;
using g3;
using GorillaLocomotion;
using HarmonyLib;
using Oculus.Platform;
using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.XR;
using Valve.VR.InteractionSystem;
using Vaga;
using Vaga.Classes;
using Vaga.Components;
using Vaga.Mods;
using Vaga.Notifications;
using Vaga.Utilities;
using static FlagCauldronColorer;
using static Vaga.Menu.Buttons;
using static Vaga.Settings;

namespace Vaga.Menu
{
    [HarmonyPatch(typeof(GTPlayer), "LateUpdate")]
    public class Main : MonoBehaviour
    {
        public static void Prefix()
        {
            // Tomas.logerror("Here the desing")
            // Initialize Menu
            try
                {
                    bool toOpen = false;
                    try
                    {
                        if (ControllerInputPoller.instance != null)
                        {
                            // Either secondary opens; hand preference only affects menu side
                            toOpen = ControllerInputPoller.instance.leftControllerSecondaryButton
                                  || ControllerInputPoller.instance.rightControllerSecondaryButton;
                        }
                    }
                    catch { toOpen = false; }
                    bool keyboardOpen = false;
                    try { keyboardOpen = UnityInput.Current != null && UnityInput.Current.GetKey(keyboardButton); } catch { }

                if (menu == null)
                {
                    if (animateTitleCoroutine != null)
                    {
                        ((MonoBehaviour)CoroutineHandler.Instance).StopCoroutine(animateTitleCoroutine);
                        animateTitleCoroutine = null;
                    }
                    if (toOpen || keyboardOpen)
                    {
                        CreateMenu();

                        if (keyboardOpen)
                        {
                            menu.AddComponent<ScaleInAnimation>();
                        }
                        RecenterMenu(rightHanded, keyboardOpen);
                        if (reference == null)
                            CreateReference(rightHanded);
                    }
                }
                else
                {
                    if (toOpen || keyboardOpen)
                        RecenterMenu(rightHanded, keyboardOpen);
                    else
                    {
                        GameObject.Find("Shoulder Camera").transform.Find("CM vcam1").gameObject.SetActive(true);

                        Rigidbody comp = menu.AddComponent(typeof(Rigidbody)) as Rigidbody;
                        comp.linearVelocity = (rightHanded ? GTPlayer.Instance.LeftHand.velocityTracker : GTPlayer.Instance.RightHand.velocityTracker).GetAverageVelocity(true, 0);

                        Destroy(menu, 2f);
                        menu = null;

                        Destroy(reference);
                        reference = null;
                    }
                }
            }
                catch (Exception exc)
                {
                    Debug.LogError(string.Format("{0} // Error initializing at {1}: {2}", PluginInfo.Name, exc.StackTrace, exc.Message));
                }

            // Constant
                try
                {
                    // Pre-Execution
                        if (fpsObject != null)
                            fpsObject.text = "FPS: " + Mathf.Ceil(1f / Time.unscaledDeltaTime).ToString();

                    // Execute Enabled Mods (central registry — O(modules) not O(all buttons))
                        try { Vaga.Mods.ModRegistry.TickAll(); } catch (Exception regEx) { Debug.LogError(PluginInfo.Name + " // ModRegistry: " + regEx.Message); }

                        // One-shot / non-registry toggles still on button.method when enabled
                        // Only scan enabled buttons that opted into per-frame method (isTogglable path)
                        var lists = buttons;
                        for (int ci = 0; ci < lists.Length; ci++)
                        {
                            var list = lists[ci];
                            for (int bi = 0; bi < list.Length; bi++)
                            {
                                var button = list[bi];
                                if (!button.enabled || button.method == null) continue;
                                // Skip if enableMethod present — those are handled by ModRegistry
                                if (button.enableMethod != null) continue;
                                try { button.method.Invoke(); }
                                catch (Exception exc)
                                {
                                    Debug.LogError(string.Format("{0} // Error with mod {1}: {2}", PluginInfo.Name, button.buttonText, exc.Message));
                                }
                            }
                        }
                } catch (Exception exc)
                {
                    Debug.LogError(string.Format("{0} // Error with executing mods at {1}: {2}", PluginInfo.Name, exc.StackTrace, exc.Message));
                }
        }
        private static IEnumerator AnimateTitle(Text text)
        {
            string targetText = "◆ MORPHINE";
            while (true)
            {
                for (int i = 0; i <= targetText.Length; i++)
                {
                    string currentText = targetText.Substring(0, i);
                    text.text = currentText;
                    yield return new WaitForSeconds(0.08f);
                }
                yield return new WaitForSeconds(0.3f);
                text.text = "";
                yield return new WaitForSeconds(0.08f);
            }
        }
        // Functions
        public static void CreateMenu()
        {
            UiTheme.Ensure();

            // Menu Holder
            menu = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.Destroy(menu.GetComponent<Rigidbody>());
            UnityEngine.Object.Destroy(menu.GetComponent<BoxCollider>());
            UnityEngine.Object.Destroy(menu.GetComponent<Renderer>());
            menu.transform.localScale = new Vector3(0.1f, 0.3f, 0.3825f);

            // Menu Background + outline — hidden on desktop (PC), shown in VR
            bool isVr = false;
            try { isVr = UnityEngine.XR.XRSettings.isDeviceActive; } catch { isVr = false; }

            menuBackground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.Destroy(menuBackground.GetComponent<Rigidbody>());
            UnityEngine.Object.Destroy(menuBackground.GetComponent<BoxCollider>());
            menuBackground.transform.parent = menu.transform;
            menuBackground.transform.rotation = Quaternion.identity;
            menuBackground.transform.localScale = menuSize;
            menuBackground.transform.position = new Vector3(0.05f, 0f, 0f);
            if (isVr)
            {
                UiTheme.ApplyToRenderer(menuBackground.GetComponent<Renderer>(), UiTheme.PanelMat);
                RoundObj(menuBackground);
            }
            else
            {
                // PC: no background panel at all
                var bgRend = menuBackground.GetComponent<Renderer>();
                if (bgRend != null) { bgRend.enabled = false; bgRend.material = null; }
                menuBackground.SetActive(false);
                try { UnityEngine.Object.Destroy(menuBackground.GetComponent<Renderer>()); } catch { }
            }

            GameObject MenuOutline = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.Destroy(MenuOutline.GetComponent<Rigidbody>());
            UnityEngine.Object.Destroy(MenuOutline.GetComponent<BoxCollider>());
            MenuOutline.transform.parent = menu.transform;
            MenuOutline.transform.rotation = Quaternion.identity;
            MenuOutline.transform.localScale = new Vector3(0.098f, 1.01f, 1.11f);
            MenuOutline.transform.position = new Vector3(0.05f, 0f, 0f);
            if (isVr)
            {
                UiTheme.ApplyToRenderer(MenuOutline.GetComponent<Renderer>(), UiTheme.OutlineMat);
                                RoundObj(MenuOutline);
            }
            else
            {
                var olRend = MenuOutline.GetComponent<Renderer>();
                if (olRend != null) olRend.enabled = false;
                MenuOutline.SetActive(false);
                try { UnityEngine.Object.Destroy(olRend); } catch { }
            }

            // Canvas
            canvasObject = new GameObject();
            canvasObject.transform.parent = menu.transform; 
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            CanvasScaler canvasScaler = canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasScaler.dynamicPixelsPerUnit = 16000f; // sharper text

            // Title
            Text text = new GameObject
            {
                transform =
        {
            parent = canvasObject.transform
        }
            }.AddComponent<Text>();
            text.font = UiTheme.TitleFont != null ? UiTheme.TitleFont : currentFont;
            text.text = "Morphine.wtf";
            text.fontSize = 2;
            text.color = Color.white;
            text.supportRichText = true;
            text.fontStyle = FontStyle.Bold;
            text.color = UiTheme.TextOn;
            text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 0;
            RectTransform component = text.GetComponent<RectTransform>();
            component.localPosition = Vector3.zero;
            component.sizeDelta = new Vector2(0.28f, 0.05f);
            component.position = new Vector3(0.06f, 0f, 0.18f);
            component.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));
            if (Settings.animateTitle)
            {
                animateTitleCoroutine = CoroutineHandler.Instance.StartCoroutine(AnimateTitle(text));
            }
            else
            {
                text.text = "Morphine.wtf";
            }
            if (menu == null)
            {
                if (animateTitleCoroutine != null)
                {
                    CoroutineHandler.Instance.StopCoroutine(animateTitleCoroutine);
                    animateTitleCoroutine = null;
                }
            }
            if (disconnectButton)
            {
                GameObject disconnectbutton = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (!UnityInput.Current.GetKey(KeyCode.Q))
                {
                    disconnectbutton.layer = 2;
                }
                UnityEngine.Object.Destroy(disconnectbutton.GetComponent<Rigidbody>());
                disconnectbutton.GetComponent<BoxCollider>().isTrigger = true;
                disconnectbutton.transform.parent = menu.transform;
                disconnectbutton.transform.rotation = Quaternion.identity;
                disconnectbutton.transform.localScale = new Vector3(0.09f, 0.53f, 0.08f);
                disconnectbutton.transform.localPosition = new Vector3(0.56f, 0f, 0.62f);
                disconnectbutton.GetComponent<Renderer>().material.color = UiTheme.ButtonOff;
                disconnectbutton.AddComponent<Classes.ButtonCollider>().relatedText = "Disconnect";
                RoundObj(disconnectbutton);

                GameObject disconnectbuttonOutline = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (!UnityInput.Current.GetKey(KeyCode.Q))
                {
                    disconnectbuttonOutline.layer = 2;
                }
                UnityEngine.Object.Destroy(disconnectbuttonOutline.GetComponent<Rigidbody>());
                disconnectbuttonOutline.GetComponent<BoxCollider>().isTrigger = true;
                disconnectbuttonOutline.transform.parent = menu.transform;
                disconnectbuttonOutline.transform.rotation = Quaternion.identity;
                disconnectbuttonOutline.transform.localScale = new Vector3(0.089f, 0.54f, 0.09f);
                disconnectbuttonOutline.transform.localPosition = new Vector3(0.56f, 0f, 0.62f);
                disconnectbuttonOutline.GetComponent<Renderer>().material.color = UiTheme.ButtonOff;
                RoundObj(disconnectbuttonOutline);

                Text disconnecttext = new GameObject
                {
                    transform =
            {
                parent = canvasObject.transform
            }
                }.AddComponent<Text>();
                disconnecttext.GetComponent<Text>().text = "Disconnect";
                disconnecttext.font = currentFont;
                disconnecttext.fontSize = 2;
                disconnecttext.color = textColors[0];
                disconnecttext.alignment = TextAnchor.MiddleCenter;
                disconnecttext.resizeTextForBestFit = true;
                disconnecttext.resizeTextMinSize = 0;

                RectTransform rectt = disconnecttext.GetComponent<RectTransform>();
                rectt.localPosition = Vector3.zero;
                rectt.sizeDelta = new Vector2(0.1f, 0.03f);
                rectt.localPosition = new Vector3(0.064f, 0f, 0.238f);
                rectt.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));
            }
            if (homeButton)
            {
                GameObject homebutton = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (!UnityInput.Current.GetKey(KeyCode.Q))
                {
                    homebutton.layer = 2;
                }
                UnityEngine.Object.Destroy(homebutton.GetComponent<Rigidbody>());
                homebutton.GetComponent<BoxCollider>().isTrigger = true;
                homebutton.transform.parent = menu.transform;
                homebutton.transform.rotation = Quaternion.identity;
                homebutton.transform.localScale = new Vector3(0.09f, 0.2f, 0.08f);
                homebutton.transform.localPosition = new Vector3(0.56f, -0.4f, 0.62f);
                homebutton.GetComponent<Renderer>().material.color = UiTheme.ButtonOff;
                homebutton.AddComponent<Classes.ButtonCollider>().relatedText = "Home";
                RoundObj(homebutton);

                GameObject homebuttonOutline = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (!UnityInput.Current.GetKey(KeyCode.Q))
                {
                    homebutton.layer = 2;
                }
                UnityEngine.Object.Destroy(homebuttonOutline.GetComponent<Rigidbody>());
                homebuttonOutline.GetComponent<BoxCollider>().isTrigger = true;
                homebuttonOutline.transform.parent = menu.transform;
                homebuttonOutline.transform.rotation = Quaternion.identity;
                homebuttonOutline.transform.localScale = new Vector3(0.089f, 0.21f, 0.09f);
                homebuttonOutline.transform.localPosition = new Vector3(0.56f, -0.4f, 0.62f);
                homebuttonOutline.GetComponent<Renderer>().material.color = UiTheme.ButtonOff;
                RoundObj(homebuttonOutline);

                Text hometext = new GameObject
                {
                    transform =
            {  
                parent = canvasObject.transform
            }
                }.AddComponent<Text>();
                hometext.GetComponent<Text>().text = "⌂";
                hometext.font = currentFont;
                hometext.fontSize = 2;
                hometext.color = textColors[0];
                hometext.alignment = TextAnchor.MiddleCenter;
                hometext.resizeTextForBestFit = true;
                hometext.resizeTextMinSize = 0;

                RectTransform rectt = hometext.GetComponent<RectTransform>();
                rectt.localPosition = Vector3.zero;
                rectt.sizeDelta = new Vector2(0.1f, 0.03f);
                rectt.localPosition = new Vector3(0.064f, -0.12f, 0.241f);
                rectt.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));
            }
            if (Settingsbutton)
            {
                GameObject settingsbutton = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (!UnityInput.Current.GetKey(KeyCode.Q))
                {
                    settingsbutton.layer = 2;
                }
                UnityEngine.Object.Destroy(settingsbutton.GetComponent<Rigidbody>());
                settingsbutton.GetComponent<BoxCollider>().isTrigger = true;
                settingsbutton.transform.parent = menu.transform;
                settingsbutton.transform.rotation = Quaternion.identity;
                settingsbutton.transform.localScale = new Vector3(0.09f, 0.2f, 0.08f);
                settingsbutton.transform.localPosition = new Vector3(0.56f, 0.4f, 0.62f);
                settingsbutton.GetComponent<Renderer>().material.color = UiTheme.ButtonOff;
                settingsbutton.AddComponent<Classes.ButtonCollider>().relatedText = "Settings";
                RoundObj(settingsbutton);

                GameObject settingsbuttonOutline = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (!UnityInput.Current.GetKey(KeyCode.Q))
                {
                    settingsbuttonOutline.layer = 2;
                }
                UnityEngine.Object.Destroy(settingsbuttonOutline.GetComponent<Rigidbody>());
                settingsbuttonOutline.GetComponent<BoxCollider>().isTrigger = true;
                settingsbuttonOutline.transform.parent = menu.transform;
                settingsbuttonOutline.transform.rotation = Quaternion.identity;
                settingsbuttonOutline.transform.localScale = new Vector3(0.089f, 0.21f, 0.09f);
                settingsbuttonOutline.transform.localPosition = new Vector3(0.56f, 0.4f, 0.62f);
                settingsbuttonOutline.GetComponent<Renderer>().material.color = UiTheme.ButtonOff;
                RoundObj(settingsbuttonOutline);

                Text settingstext = new GameObject
                {
                    transform =
            {
                parent = canvasObject.transform
            }
                }.AddComponent<Text>();
                settingstext.GetComponent<Text>().text = "✎";
                settingstext.font = currentFont;
                settingstext.fontSize = 1;
                settingstext.color = textColors[0];
                settingstext.alignment = TextAnchor.MiddleCenter;
                settingstext.resizeTextForBestFit = true;
                settingstext.resizeTextMinSize = 0;

                RectTransform rectt = settingstext.GetComponent<RectTransform>();
                rectt.localPosition = Vector3.zero;
                rectt.sizeDelta = new Vector2(0.1f, 0.03f);
                rectt.localPosition = new Vector3(0.064f, 0.12f, 0.238f);
                rectt.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));
            }

            GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!UnityInput.Current.GetKey(KeyCode.Q))
            {
                gameObject.layer = 2;
            }
            UnityEngine.Object.Destroy(gameObject.GetComponent<Rigidbody>());
            gameObject.GetComponent<BoxCollider>().isTrigger = true;
            gameObject.transform.parent = menu.transform;
            gameObject.transform.rotation = Quaternion.identity;
            gameObject.transform.localScale = new Vector3(0.05f, 0.14f, 0.08f);
            gameObject.transform.localPosition = new Vector3(0.56f, 0.55f, -0.02f);
            gameObject.GetComponent<Renderer>().material.color = UiTheme.ButtonOff;
            gameObject.AddComponent<Classes.ButtonCollider>().relatedText = "PreviousPage";
            gameObject.AddComponent<ButtonGradiant>();
            RoundObj(gameObject);

            GameObject Outline1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!UnityInput.Current.GetKey(KeyCode.Q))
            {
                gameObject.layer = 2;
            }
            UnityEngine.Object.Destroy(Outline1.GetComponent<Rigidbody>());
            Outline1.GetComponent<BoxCollider>().isTrigger = true;
            Outline1.transform.parent = menu.transform;
            Outline1.transform.rotation = Quaternion.identity;
            Outline1.transform.localScale = new Vector3(0.048f, 0.15f, 0.085f);
            Outline1.transform.localPosition = new Vector3(0.56f, 0.55f, -0.02f);
            Outline1.GetComponent<Renderer>().material.color = UiTheme.ButtonOff;
            Outline1.AddComponent<ButtonGradiant>();
            if (Settings.PageButtonGradiant)
            {
            }
            Outline1.AddComponent<Classes.ButtonCollider>().relatedText = "PreviousPage";
            RoundObj(Outline1);

            text = new GameObject
            {
                transform =
        {
            parent = canvasObject.transform
        }
            }.AddComponent<Text>();
            text.font = currentFont;
            text.text = "<";
            text.fontSize = 2;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 0;
            component = text.GetComponent<RectTransform>();
            component.localPosition = Vector3.zero;
            component.sizeDelta = new Vector2(0.2f, 0.03f);
            component.localPosition = new Vector3(0.064f, 0.175f, -0.008f);
            component.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));

            gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!UnityInput.Current.GetKey(KeyCode.Q))
            {
                gameObject.layer = 2;
            }
            UnityEngine.Object.Destroy(gameObject.GetComponent<Rigidbody>());
            gameObject.GetComponent<BoxCollider>().isTrigger = true;
            gameObject.transform.parent = menu.transform;
            gameObject.transform.rotation = Quaternion.identity;
            gameObject.transform.localScale = new Vector3(0.05f, 0.14f, 0.08f);
            gameObject.transform.localPosition = new Vector3(0.56f, -0.55f, -0.02f);
            gameObject.GetComponent<Renderer>().material.color = UiTheme.ButtonOff;
            gameObject.AddComponent<Classes.ButtonCollider>().relatedText = "NextPage";
            gameObject.AddComponent<ButtonGradiant>();
            RoundObj(gameObject);

            GameObject Outline = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!UnityInput.Current.GetKey(KeyCode.Q))
            {
                gameObject.layer = 2;
            }
            UnityEngine.Object.Destroy(Outline.GetComponent<Rigidbody>());
            Outline.GetComponent<BoxCollider>().isTrigger = true;
            Outline.transform.parent = menu.transform;
            Outline.transform.rotation = Quaternion.identity;
            Outline.transform.localScale = new Vector3(0.048f, 0.15f, 0.085f);
            Outline.transform.localPosition = new Vector3(0.56f, -0.55f, -0.02f);
            Outline.GetComponent<Renderer>().material.color = UiTheme.ButtonOff;
            Outline.AddComponent<ButtonGradiant>();
            if (Settings.PageButtonGradiant)
            {
                
            }
            Outline.AddComponent<Classes.ButtonCollider>().relatedText = "NextPage";
            RoundObj(Outline);

            text = new GameObject
            {
                transform =
        {
            parent = canvasObject.transform
        }
            }.AddComponent<Text>();
            text.font = currentFont;
            text.text = ">";
            text.fontSize = 2;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 0;
            component = text.GetComponent<RectTransform>();
            component.localPosition = Vector3.zero;
            component.sizeDelta = new Vector2(0.2f, 0.03f);
            component.localPosition = new Vector3(0.064f, -0.175f, -0.008f);
            component.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));

            ButtonInfo[] activeButtons = buttons[currentCategory].Skip(pageNumber * buttonsPerPage).Take(buttonsPerPage).ToArray();
            for (int i = 0; i < activeButtons.Length; i++)
                CreateButton(i * 0.1f, activeButtons[i]);
        }

        public static void CreateButton(float offset, ButtonInfo method)
        {
            GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!UnityInput.Current.GetKey(KeyCode.Q))
            {
                gameObject.layer = 2;
            }
            UnityEngine.Object.Destroy(gameObject.GetComponent<Rigidbody>());
            gameObject.GetComponent<BoxCollider>().isTrigger = true;
            gameObject.transform.parent = menu.transform;
            gameObject.transform.rotation = Quaternion.identity;
            gameObject.transform.localScale = new Vector3(0.06f, 0.9f, 0.08f);
            gameObject.transform.localPosition = new Vector3(0.56f, 0f, 0.34f - offset);
            gameObject.GetComponent<Renderer>().material.color = UiTheme.ButtonOff;
            gameObject.AddComponent<Classes.ButtonCollider>().relatedText = method.buttonText;
            gameObject.AddComponent<ButtonGradiant>();
            RoundObj(gameObject);

            GameObject ButtonOutline = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!UnityInput.Current.GetKey(KeyCode.Q))
            {
                ButtonOutline.layer = 2;
            }
            UnityEngine.Object.Destroy(ButtonOutline.GetComponent<Rigidbody>());
            ButtonOutline.GetComponent<BoxCollider>().isTrigger = true;
            ButtonOutline.transform.parent = menu.transform;
            ButtonOutline.transform.rotation = Quaternion.identity;
            ButtonOutline.transform.localScale = new Vector3(0.058f, 0.915f, 0.09f);
            ButtonOutline.transform.localPosition = new Vector3(0.56f, 0f, 0.34f - offset);
            ButtonOutline.AddComponent<Classes.ButtonCollider>().relatedText = method.buttonText;
            ButtonOutline.GetComponent<Renderer>().material.color = UiTheme.ButtonOff;
            ButtonOutline.AddComponent<ButtonGradiant>();
            RoundObj(ButtonOutline);

            ColorChanger colorChanger = gameObject.AddComponent<ColorChanger>();
            if (method.enabled)
            {
                colorChanger.colorInfo = buttonColors[1];
            }
            else
            {
                colorChanger.colorInfo = buttonColors[0];
            }
            colorChanger.Start();

            Text text = new GameObject
            {
                transform =
                {
                    parent = canvasObject.transform
                }
            }.AddComponent<Text>();
            if (method.overlapText != null)
                text.text = method.overlapText;
            text.font = currentFont;
            text.text = method.buttonText;
            text.supportRichText = true;
            text.fontSize = 3;
            if (method.enabled)
            {
                text.color = Settings.textColors[1];
            }
            else
            {
                text.color = Settings.textColors[0];
            }
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Italic;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 0;
            RectTransform component = text.GetComponent<RectTransform>();
            component.localPosition = Vector3.zero;
            component.sizeDelta = new Vector2(.1f, .01f);
            component.localPosition = new Vector3(.0595f, 0, 0.13f - offset / 2.6f);
            component.rotation = Quaternion.Euler(new Vector3(180f, 90f, 90f));
        }

        public static void RecreateMenu(bool isInitialPosition = false)
        {
            if (menu != null)
            {
                Destroy(menu);
                menu = null;

                CreateMenu();
                RecenterMenu(rightHanded, UnityInput.Current.GetKey(keyboardButton));
            }
        }


        public static void RoundObj(GameObject toRound, float bevel = 0.025f, string sides = "both")
        {
            Renderer ToRoundRenderer = toRound.GetComponent<Renderer>();

            GameObject BaseA = GameObject.CreatePrimitive(PrimitiveType.Cube);
            BaseA.GetComponent<Renderer>().enabled = ToRoundRenderer.enabled;
            UnityEngine.Object.Destroy(BaseA.GetComponent<Collider>());
            BaseA.transform.parent = menu.transform;
            BaseA.transform.rotation = Quaternion.identity;
            BaseA.transform.localPosition = toRound.transform.localPosition;
            BaseA.transform.localScale = toRound.transform.localScale + new Vector3(0f, bevel * -2.55f, 0f);

            GameObject BaseB = GameObject.CreatePrimitive(PrimitiveType.Cube);
            BaseB.GetComponent<Renderer>().enabled = ToRoundRenderer.enabled;
            UnityEngine.Object.Destroy(BaseB.GetComponent<Collider>());
            BaseB.transform.parent = menu.transform;
            BaseB.transform.rotation = Quaternion.identity;
            BaseB.transform.localPosition = toRound.transform.localPosition;
            BaseB.transform.localScale = toRound.transform.localScale + new Vector3(0f, 0f, -bevel * 2f);

            List<GameObject> ToChange = new List<GameObject> { BaseA, BaseB };

            if (sides == "both" || sides == "bottom" || sides == "left")
            {
                GameObject RoundCornerD = CreateRoundedCorner(toRound, ToRoundRenderer, bevel,
                    new Vector3(0f, -(toRound.transform.localScale.y / 2f) + (bevel * 1.275f), -(toRound.transform.localScale.z / 2f) + bevel));

                GameObject RoundCornerB = CreateRoundedCorner(toRound, ToRoundRenderer, bevel,
                    new Vector3(0f, -(toRound.transform.localScale.y / 2f) + (bevel * 1.275f), (toRound.transform.localScale.z / 2f) - bevel));

                ToChange.Add(RoundCornerD);
                ToChange.Add(RoundCornerB);
            }

            if (sides == "both" || sides == "top" || sides == "right")
            {
                GameObject RoundCornerC = CreateRoundedCorner(toRound, ToRoundRenderer, bevel,
                    new Vector3(0f, (toRound.transform.localScale.y / 2f) - (bevel * 1.275f), -(toRound.transform.localScale.z / 2f) + bevel));

                GameObject RoundCornerA = CreateRoundedCorner(toRound, ToRoundRenderer, bevel,
                    new Vector3(0f, (toRound.transform.localScale.y / 2f) - (bevel * 1.275f), (toRound.transform.localScale.z / 2f) - bevel));

                ToChange.Add(RoundCornerC);
                ToChange.Add(RoundCornerA);
            }

            foreach (GameObject Changed in ToChange)
            {
                ColorRoundObj TargetChanger = Changed.AddComponent<ColorRoundObj>();
                TargetChanger.targetRenderer = ToRoundRenderer;
                TargetChanger.Start();
            }

            ToRoundRenderer.enabled = false;
        }
        
        

        private static GameObject CreateRoundedCorner(GameObject parent, Renderer renderer, float bevel, Vector3 localPositionOffset)
        {

            GameObject corner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            corner.GetComponent<Renderer>().enabled = renderer.enabled;
            UnityEngine.Object.Destroy(corner.GetComponent<Collider>());

            corner.transform.parent = menu.transform;
            corner.transform.rotation = Quaternion.identity * Quaternion.Euler(0f, 0f, 90f);
            corner.transform.localPosition = parent.transform.localPosition + localPositionOffset;
            corner.transform.localScale = new Vector3(bevel * 2.55f, parent.transform.localScale.x / 2f, bevel * 2f);

            return corner;
        }



        public static void RoundLeftObj(GameObject toRound)
        {
            float Bevel = 0.1f;

            Renderer ToRoundRenderer = toRound.GetComponent<Renderer>();
            GameObject BaseA = GameObject.CreatePrimitive(PrimitiveType.Cube);
            BaseA.GetComponent<Renderer>().enabled = ToRoundRenderer.enabled;
            UnityEngine.Object.Destroy(BaseA.GetComponent<Collider>());

            BaseA.transform.parent = menu.transform;
            BaseA.transform.rotation = Quaternion.identity;
            BaseA.transform.localPosition = toRound.transform.localPosition;
            BaseA.transform.localScale = toRound.transform.localScale;

            GameObject BaseB = GameObject.CreatePrimitive(PrimitiveType.Cube);
            BaseB.GetComponent<Renderer>().enabled = ToRoundRenderer.enabled;
            UnityEngine.Object.Destroy(BaseB.GetComponent<Collider>());

            BaseB.transform.parent = menu.transform;
            BaseB.transform.rotation = Quaternion.identity;
            BaseB.transform.localPosition = toRound.transform.localPosition;
            BaseB.transform.localScale = toRound.transform.localScale + new Vector3(0f, 0f, -Bevel * 2f);

            GameObject RoundCornerA = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            RoundCornerA.GetComponent<Renderer>().enabled = ToRoundRenderer.enabled;
            UnityEngine.Object.Destroy(RoundCornerA.GetComponent<Collider>());

            RoundCornerA.transform.parent = menu.transform;
            RoundCornerA.transform.rotation = Quaternion.identity * Quaternion.Euler(0f, 0f, 90f);

            RoundCornerA.transform.localPosition = toRound.transform.localPosition + new Vector3(0f, (toRound.transform.localScale.y / 2f) - (Bevel * 1.275f), -(toRound.transform.localScale.z / 2f) + Bevel);
            RoundCornerA.transform.localScale = new Vector3(Bevel * 2.55f, toRound.transform.localScale.x / 2f, Bevel * 2f);

            GameObject RoundCornerB = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            RoundCornerB.GetComponent<Renderer>().enabled = ToRoundRenderer.enabled;
            UnityEngine.Object.Destroy(RoundCornerB.GetComponent<Collider>());

            RoundCornerB.transform.parent = menu.transform;
            RoundCornerB.transform.rotation = Quaternion.identity * Quaternion.Euler(0f, 0f, 90f);

            RoundCornerB.transform.localPosition = toRound.transform.localPosition + new Vector3(0f, -(toRound.transform.localScale.y / 2f) + (Bevel * 1.275f), -(toRound.transform.localScale.z / 2f) + Bevel);
            RoundCornerB.transform.localScale = new Vector3(Bevel * 2.55f, toRound.transform.localScale.x / 2f, Bevel * 2f);

            GameObject[] ToChange = new GameObject[]
            {
        BaseA,
        BaseB,
        RoundCornerA,
        RoundCornerB
            };

            foreach (GameObject Changed in ToChange)
            {
                ColorRoundObj TargetChanger = Changed.AddComponent<ColorRoundObj>();
                TargetChanger.targetRenderer = ToRoundRenderer;

                TargetChanger.Start();
            }

            ToRoundRenderer.enabled = false;
        }

        public static void RecenterMenu(bool isRightHanded, bool isKeyboardCondition, bool isInitialPosition = false)
        {
            Quaternion rotation;
            if (!isKeyboardCondition)
            {
                Vector3 position;
                Quaternion val;
                if (!isRightHanded)
                {
                    position = GorillaTagger.Instance.leftHandTransform.position;
                    val = GorillaTagger.Instance.leftHandTransform.rotation;
                }
                else
                {
                    position = GorillaTagger.Instance.rightHandTransform.position;
                    rotation = GorillaTagger.Instance.rightHandTransform.rotation;
                    Vector3 eulerAngles = rotation.eulerAngles;
                    eulerAngles += new Vector3(0f, 0f, 180f);
                    val = Quaternion.Euler(eulerAngles);
                }
                return;
            }
            else
            {
                try
                {
                    TPC = GameObject.Find("Player Objects/Third Person Camera/Shoulder Camera").GetComponent<Camera>();
                }
                catch { }

                GameObject.Find("Shoulder Camera").transform.Find("CM vcam1").gameObject.SetActive(false);

                if (TPC != null)
                {
                    TPC.transform.position = new Vector3(-999f, -999f, -999f);
                    TPC.transform.rotation = Quaternion.identity;
                    GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bg.transform.localScale = new Vector3(10f, 10f, 0.01f);
                    bg.transform.transform.position = TPC.transform.position + TPC.transform.forward;
                    Color realcolor = backgroundColor.GetCurrentColor();
                    bg.GetComponent<Renderer>().material.color = new Color32((byte)(realcolor.r * 50), (byte)(realcolor.g * 50), (byte)(realcolor.b * 50), 255);
                    Destroy(bg, 0.05f);
                    menu.transform.parent = TPC.transform;
                    menu.transform.position = TPC.transform.position + (TPC.transform.forward * 0.5f) + (TPC.transform.up * -0.02f);
                    menu.transform.rotation = TPC.transform.rotation * Quaternion.Euler(-90f, 90f, 0f);

                    if (reference != null)
                    {
                        if (Mouse.current.leftButton.isPressed)
                        {
                            Ray ray = TPC.ScreenPointToRay(Mouse.current.position.ReadValue());
                            bool hitButton = Physics.Raycast(ray, out RaycastHit hit, 100);
                            if (hitButton)
                            {
                                Classes.ButtonCollider collide = hit.transform.gameObject.GetComponent<Classes.ButtonCollider>();
                                collide?.OnTriggerEnter(buttonCollider);
                            }
                        }
                        else
                            reference.transform.position = new Vector3(999f, -999f, -999f);
                    }
                }
            }
        }

        public static void CreateReference(bool isRightHanded)
        {
            reference = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            reference.transform.parent = isRightHanded ? GorillaTagger.Instance.leftHandTransform : GorillaTagger.Instance.rightHandTransform;
            reference.GetComponent<Renderer>().material.color = backgroundColor.colors[0].color;
            reference.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            reference.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            buttonCollider = reference.GetComponent<SphereCollider>();

            ColorChanger colorChanger = reference.AddComponent<ColorChanger>();
            colorChanger.colors = backgroundColor;
        }

        public static void Toggle(string buttonText)
        {
            if (buttonText == "Home")
            {
                currentCategory = 0;
            }
            if (buttonText == "Settings")
            {
                currentCategory = 1;
            }
            int lastPage = ((buttons[currentCategory].Length + buttonsPerPage - 1) / buttonsPerPage) - 1;
            if (buttonText == "PreviousPage")
            {
                pageNumber--;
                if (pageNumber < 0)
                    pageNumber = lastPage;
            } else
            {
                if (buttonText == "NextPage")
                {
                    pageNumber++;
                    if (pageNumber > lastPage)
                        pageNumber = 0;
                } else
                {
                    ButtonInfo target = GetIndex(buttonText);
                    if (target != null)
                    {
                        if (target.isTogglable)
                        {
                            target.enabled = !target.enabled;
                            if (target.enabled)
                            {
                                NotifiLib.SendNotification("<color=grey>[</color><color=green>ENABLE</color><color=grey>]</color> " + target.toolTip);
                                if (target.enableMethod != null)
                                    try { target.enableMethod.Invoke(); } catch { }
                            }
                            else
                            {
                                NotifiLib.SendNotification("<color=grey>[</color><color=red>DISABLE</color><color=grey>]</color> " + target.toolTip);
                                if (target.disableMethod != null)
                                    try { target.disableMethod.Invoke(); } catch { }
                            }
                        }
                        else
                        {
                            NotifiLib.SendNotification("<color=grey>[</color><color=green>ENABLE</color><color=grey>]</color> " + target.toolTip);
                            if (target.method != null)
                                try { target.method.Invoke(); } catch { }
                        }
                    }
                    else
                        Debug.LogError(buttonText + " does not exist");
                }
            }
            RecreateMenu();
        }

        private static readonly Dictionary<string, (int Category, int Index)> cacheGetIndex = new Dictionary<string, (int Category, int Index)>(); // Looping through 800 elements is not a light task :/
        public static ButtonInfo GetIndex(string buttonText)
        {
            if (buttonText == null)
                return null;

            if (cacheGetIndex.ContainsKey(buttonText))
            {
                var CacheData = cacheGetIndex[buttonText];
                try
                {
                    if (buttons[CacheData.Category][CacheData.Index].buttonText == buttonText)
                        return buttons[CacheData.Category][CacheData.Index];
                }
                catch { cacheGetIndex.Remove(buttonText); }
            }

            int categoryIndex = 0;
            foreach (ButtonInfo[] buttons in buttons)
            {
                int buttonIndex = 0;
                foreach (ButtonInfo button in buttons)
                {
                    if (button.buttonText == buttonText)
                    {
                        try
                        {
                            cacheGetIndex.Add(buttonText, (categoryIndex, buttonIndex));
                        }
                        catch
                        {
                            if (cacheGetIndex.ContainsKey(buttonText))
                                cacheGetIndex.Remove(buttonText);
                        }

                        return button;
                    }
                    buttonIndex++;
                }
                categoryIndex++;
            }

            return null;
        }

        public static Vector3 RandomVector3(float range = 1f) =>
            new Vector3(UnityEngine.Random.Range(-range, range),
                        UnityEngine.Random.Range(-range, range),
                        UnityEngine.Random.Range(-range, range));

        public static Quaternion RandomQuaternion(float range = 360f) =>
            Quaternion.Euler(UnityEngine.Random.Range(0f, range),
                        UnityEngine.Random.Range(0f, range),
                        UnityEngine.Random.Range(0f, range));

        public static Color RandomColor(byte range = 255, byte alpha = 255) =>
            new Color32((byte)UnityEngine.Random.Range(0, range),
                        (byte)UnityEngine.Random.Range(0, range),
                        (byte)UnityEngine.Random.Range(0, range),
                        alpha);

        public static (Vector3 position, Quaternion rotation, Vector3 up, Vector3 forward, Vector3 right) TrueLeftHand()
        {
            Quaternion rot = GorillaTagger.Instance.leftHandTransform.rotation * GTPlayer.Instance.LeftHand.handRotOffset;
            return (GorillaTagger.Instance.leftHandTransform.position + GorillaTagger.Instance.leftHandTransform.rotation * GTPlayer.Instance.LeftHand.handOffset, rot, rot * Vector3.up, rot * Vector3.forward, rot * Vector3.right);
        }

        public static (Vector3 position, Quaternion rotation, Vector3 up, Vector3 forward, Vector3 right) TrueRightHand()
        {
            Quaternion rot = GorillaTagger.Instance.rightHandTransform.rotation * GTPlayer.Instance.RightHand.handRotOffset;
            return (GorillaTagger.Instance.rightHandTransform.position + GorillaTagger.Instance.rightHandTransform.rotation * GTPlayer.Instance.RightHand.handOffset, rot, rot * Vector3.up, rot * Vector3.forward, rot * Vector3.right);
        }

        public static void WorldScale(GameObject obj, Vector3 targetWorldScale)
        {
            Vector3 parentScale = obj.transform.parent.lossyScale;
            obj.transform.localScale = new Vector3(
                targetWorldScale.x / parentScale.x,
                targetWorldScale.y / parentScale.y,
                targetWorldScale.z / parentScale.z
            );
        }
        public static TextMeshPro? motdHeading;
        public static TextMeshPro? motdBody;
        public static TextMeshPro? cocHeading;
        public static TextMeshPro? cocBody;
        public static TextMeshPro? gameModeText;
        public static GameObject? ThirdCam;
        public static IEnumerator GetObjects()
        {
            var obj1 = GameObject.Find("Environment Objects/LocalObjects_Prefab/TreeRoom/motdHeadingText");
            if (obj1 != null) motdHeading = obj1.GetComponent<TextMeshPro>();
            var obj2 = GameObject.Find("Environment Objects/LocalObjects_Prefab/TreeRoom/motdBodyText");
            if (obj2 != null) motdBody = obj2.GetComponent<TextMeshPro>();
            var obj3 = GameObject.Find("Environment Objects/LocalObjects_Prefab/TreeRoom/CodeOfConductHeadingText");
            if (obj3 != null) cocHeading = obj3.GetComponent<TextMeshPro>();
            var obj4 = GameObject.Find("Environment Objects/LocalObjects_Prefab/TreeRoom/COCBodyText_TitleData");
            if (obj4 != null) cocBody = obj4.GetComponent<TextMeshPro>();
            var obj5 = GameObject.Find("Environment Objects/LocalObjects_Prefab/TreeRoom/GameModes Title Text");
            if (obj5 != null) gameModeText = obj5.GetComponent<TextMeshPro>();

            ThirdCam = GameObject.Find("Player Objects/Third Person Camera/Shoulder Camera");

            yield return null;
        }
        public static Material? originalMat1;
        public static Material? originalMat2;
        public static void ChangeBoardMaterial(string parentPath, string boardID, int targetIndex, Material newMaterial, ref Material originalMat)
        {
            GameObject parent = GameObject.Find(parentPath);
            if (parent == null)
                return;
            int currentIndex = 0;
            for (int i = 0; i < parent.transform.childCount; i++)
            {
                GameObject childObj = parent.transform.GetChild(i).gameObject;
                if (childObj.name.Contains(boardID))
                {
                    currentIndex++;
                    if (currentIndex == targetIndex)
                    {
                        Renderer renderer = childObj.GetComponent<Renderer>();
                        if (originalMat == null)
                            originalMat = renderer.material;
                        renderer.material = newMaterial;
                        break;
                    }
                }
            }
        }
        public static Material Url2Mat(string url)
        {
            byte[] imageData;
            using (var httpClient = new HttpClient())
            {
                HttpResponseMessage response;
                response = httpClient.GetAsync(url).Result;
                imageData = response.Content.ReadAsByteArrayAsync().Result;
            }

            var texture = new Texture2D(2, 2);
            ImageConversion.LoadImage(texture, imageData);
            texture.Apply();

            var material = new Material(Shader.Find("GorillaTag/UberShader"))
            {
                shaderKeywords = new[] { "_USE_TEXTURE" },
                mainTexture = texture
            };
            return material;
        }
        public static string ClrToHex(Color c)
        {
            Color32 c32 = c;
            return $"{c32.r:X2}{c32.g:X2}{c32.b:X2}";
        }
        public static class GradientText
        {
            public static string MakeGradient(string hexStart, string hexEnd, string text)
            {
                Color start = HexToColor(hexStart);
                Color end = HexToColor(hexEnd);
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                int len = text.Length;
                for (int i = 0; i < len; i++)
                {
                    float t = (float)i / Mathf.Max(len - 1, 1);
                    Color c = Color.Lerp(start, end, t);
                    sb.Append($"<color=#{ClrToHex(c)}>{text[i]}</color>");
                }
                return sb.ToString();
            }
            public static string MakeAnimatedGradient(string hexStart, string hexEnd, string text, float time, float speed = 1f)
            {
                Color start = HexToColor(hexStart);
                Color end = HexToColor(hexEnd);
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                int len = text.Length;
                float offset = (time * speed) % 1f;

                for (int i = 0; i < len; i++)
                {
                    float t = ((float)i / Mathf.Max(len - 1, 1) - offset) % 1f;
                    if (t < 0) t += 1f;
                    Color c = Color.Lerp(start, end, t);
                    sb.Append($"<color=#{ClrToHex(c)}>{text[i]}</color>");
                }
                return sb.ToString();
            }
            public static Color HexToColor(string hex)
            {
                if (hex.StartsWith("#")) hex = hex.Substring(1);
                byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                return new Color32(r, g, b, 255);
            }
        }

        public static Material fmby = Url2Mat("https://i.ebayimg.com/images/g/XI8AAOSwwvRlMHkz/s-l1200.jpg");
        public static Material SherbMat = Url2Mat("https://raw.githubusercontent.com/Cha554/Stone-Networking/main/Stone/Sherbert.jpg");
        public static Material AngrySherbMat = Url2Mat("https://raw.githubusercontent.com/Cha554/Stone-Networking/main/Sherbert(1).jpg");

        public static void FixStickyColliders(GameObject platform)
        {
            Vector3[] localPositions = new Vector3[]
            {
                new Vector3(0, 1f, 0),
                new Vector3(0, -1f, 0),
                new Vector3(1f, 0, 0),
                new Vector3(-1f, 0, 0),
                new Vector3(0, 0, 1f),
                new Vector3(0, 0, -1f)
            };
            Quaternion[] localRotations = new Quaternion[]
            {
                Quaternion.Euler(90, 0, 0),
                Quaternion.Euler(-90, 0, 0),
                Quaternion.Euler(0, -90, 0),
                Quaternion.Euler(0, 90, 0),
                Quaternion.identity,
                Quaternion.Euler(0, 180, 0)
            };
            for (int i = 0; i < localPositions.Length; i++)
            {
                GameObject side = GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    if (platform.GetComponent<GorillaSurfaceOverride>() != null)
                    {
                        side.AddComponent<GorillaSurfaceOverride>().overrideIndex = platform.GetComponent<GorillaSurfaceOverride>().overrideIndex;
                    }
                }
                catch { }
                float size = 0.025f;
                side.transform.SetParent(platform.transform);
                side.transform.position = localPositions[i] * (size / 2);
                side.transform.rotation = localRotations[i];
                WorldScale(side, new Vector3(size, size, 0.01f));
                side.GetComponent<Renderer>().enabled = false;
            }
        }

        private static int? noInvisLayerMask;
        public static int NoInvisLayerMask()
        {
            noInvisLayerMask ??= ~(
                1 << LayerMask.NameToLayer("TransparentFX") |
                1 << LayerMask.NameToLayer("Ignore Raycast") |
                1 << LayerMask.NameToLayer("Zone") |
                1 << LayerMask.NameToLayer("Gorilla Trigger") |
                1 << LayerMask.NameToLayer("Gorilla Boundary") |
                1 << LayerMask.NameToLayer("GorillaCosmetics") |
                1 << LayerMask.NameToLayer("GorillaParticle"));

            return noInvisLayerMask ?? GTPlayer.Instance.locomotionEnabledLayers;
        }

        public static bool gunLocked;
        public static VRRig lockTarget;

        public static (RaycastHit Ray, GameObject NewPointer) RenderGun(int? overrideLayerMask = null)
        {
            Transform GunTransform = GorillaTagger.Instance.rightHandTransform;

            Vector3 StartPosition = GunTransform.position;
            Vector3 Direction = GunTransform.forward;

            Physics.Raycast(StartPosition + Direction / 4f, Direction, out var Ray, 512f, overrideLayerMask ?? NoInvisLayerMask());
            Vector3 EndPosition = gunLocked ? lockTarget.transform.position : Ray.point;

            if (EndPosition == Vector3.zero)
                EndPosition = StartPosition + Direction * 512f;

            if (GunPointer == null)
                GunPointer = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            GunPointer.SetActive(true);
            GunPointer.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            GunPointer.transform.position = EndPosition;

            Renderer PointerRenderer = GunPointer.GetComponent<Renderer>();
            PointerRenderer.material.shader = Shader.Find("GUI/Text Shader");
            PointerRenderer.material.color = gunLocked || ControllerInputPoller.TriggerFloat(XRNode.RightHand) > 0.5f ? buttonColors[1].GetCurrentColor() : buttonColors[0].GetCurrentColor();

            Destroy(GunPointer.GetComponent<Collider>());

            if (GunLine == null)
            {
                GameObject line = new GameObject("iiMenu_GunLine");
                GunLine = line.AddComponent<LineRenderer>();
            }

            GunLine.gameObject.SetActive(true);
            GunLine.material.shader = Shader.Find("GUI/Text Shader");
            GunLine.startColor = backgroundColor.GetCurrentColor();
            GunLine.endColor = backgroundColor.GetCurrentColor(0.5f);
            GunLine.startWidth = 0.025f;
            GunLine.endWidth = 0.025f;
            GunLine.positionCount = 2;
            GunLine.useWorldSpace = true;

            GunLine.SetPosition(0, StartPosition);
            GunLine.SetPosition(1, EndPosition);

            return (Ray, GunPointer);
        }
        public class GradientSetter : MonoBehaviour
        {
            [Header("Color Settings")]
            [SerializeField, Range(0f, 2f)] public float brightness = 1f;
            [SerializeField] public bool isVertical = false;
            [SerializeField, Range(0f, 10f)] public float gradientOffset = 1f;
            [SerializeField, Range(0f, 10f)] public float startOffset = 0f;
            private Renderer rend;
            public Material cachedMaterial;
            private Texture2D gradientTexture;
            private Color[] pixels;
            private const int width = 64;
            private const int height = 64;
            private Color lastColor1;
            private Color lastColor2;
            private bool needsUpdate = true;
            private float updateTimer = 0f;
            private const float updateInterval = 0.033f;
            private bool initialized = false;
            private bool isCylinder = false;
            private void Start()
            {
                rend = GetComponent<Renderer>();
                if (rend == null) return;
                isCylinder = GetComponent<MeshFilter>()?.sharedMesh.name.Contains("Cylinder") ?? false;
                MeshFilter mf = GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Mesh m = Instantiate(mf.sharedMesh);
                    Vector3[] verts = m.vertices;
                    Vector2[] uvs = m.uv;
                    Vector3 min = m.bounds.min;
                    Vector3 size = m.bounds.size;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        float u = size.x > 0.001f ? (verts[i].x - min.x) / size.x : 0f;
                        float v = size.y > 0.001f ? (verts[i].y - min.y) / size.y : 0f;
                        float z = size.z > 0.001f ? (verts[i].z - min.z) / size.z : 0f;
                        if (isCylinder)
                        {
                            uvs[i] = new Vector2(u, z);
                        }
                        else
                        {
                            uvs[i] = new Vector2(z, v);
                        }
                    }
                    m.uv = uvs;
                    mf.mesh = m;
                }
                cachedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                rend.material = cachedMaterial;
                CreateGradientTexture();
                initialized = true;
                lastColor1 = new Color(20f / 255f, 10f / 255f, 30f / 255f);
                lastColor2 = new Color(20f / 255f, 10f / 255f, 30f / 255f);
                UpdateGradientTexture();
            }
            public static GameObject BoardGradientObject = null;
            public static Material BoardMat;
            private void Update()
            {
                if (gameObject == BoardGradientObject)
                    BoardMat = cachedMaterial;
                if (!initialized || !isActiveAndEnabled) return;
                if (rend != null && rend.material != cachedMaterial)
                    rend.material = cachedMaterial;
                updateTimer += Time.deltaTime;
                if (updateTimer >= updateInterval)
                {
                    updateTimer = 0f;
                    Color color1 = new Color(20f / 255f, 10f / 255f, 30f / 255f);
                    Color color2 = new Color(20f / 255f, 10f / 255f, 30f / 255f);
                    if (Vector4.Distance(lastColor1, color1) > 0.02f || Vector4.Distance(lastColor2, color2) > 0.02f)
                    {
                        lastColor1 = color1;
                        lastColor2 = color2;
                        needsUpdate = true;
                    }
                    if (needsUpdate)
                    {
                        UpdateGradientTexture();
                        needsUpdate = false;
                    }
                }
            }
            private void CreateGradientTexture()
            {
                gradientTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
                gradientTexture.filterMode = FilterMode.Bilinear;
                gradientTexture.wrapMode = isCylinder ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                pixels = new Color[width * height];
                cachedMaterial.color = Color.white;
                cachedMaterial.mainTexture = gradientTexture;
            }
            private void UpdateGradientTexture()
            {
                if (gradientTexture == null) return;
                Color color1 = lastColor2;
                Color color2 = lastColor1;
                color1.a = 1f;
                color2.a = 1f;
                int index = 0;
                if (isCylinder)
                {
                    for (int y = 0; y < height; y++)
                    {
                        float t = (float)y / height;
                        Color lineColor = Color.Lerp(color1, color2, t);
                        for (int x = 0; x < width; x++)
                            pixels[index++] = lineColor;
                    }
                }
                else if (isVertical)
                {
                    for (int y = 0; y < height; y++)
                    {
                        float t = (float)y / height;
                        Color lineColor = Color.Lerp(color1, color2, t);
                        for (int x = 0; x < width; x++)
                            pixels[index++] = lineColor;
                    }
                }
                else
                {
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            float t = (float)x / width;
                            pixels[index++] = Color.Lerp(color1, color2, t);
                        }
                    }
                }
                gradientTexture.SetPixels(pixels);
                gradientTexture.Apply(false);
            }
            public void SetBrightness(float value)
            {
                brightness = Mathf.Max(0f, value);
                needsUpdate = true;
            }
            private void OnDestroy()
            {
                if (gradientTexture != null) Destroy(gradientTexture);
                if (cachedMaterial != null) Destroy(cachedMaterial);
            }
        }
        public class ScaleInAnimation : MonoBehaviour
        {
            [Header("Settings")]
            [SerializeField] public bool reverse = false;
            [SerializeField] public float duration = 0.4f;
            [SerializeField] public System.Action onComplete;
            private Vector3 startScale;
            private Vector3 targetScale;
            private float elapsed;
            private bool initialized;
            private void Awake() { Initialize(); }
            private void Initialize()
            {
                if (initialized) return;
                if (!reverse)
                {
                    targetScale = transform.localScale;
                    startScale = Vector3.zero;
                    transform.localScale = startScale;
                }
                else
                {
                    startScale = transform.localScale;
                    targetScale = Vector3.zero;
                }
                elapsed = 0f;
                initialized = true;
            }
            private void Update()
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easedT = EaseInOutCubic(t);
                transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, easedT);
                if (t >= 1f)
                {
                    transform.localScale = targetScale;
                    onComplete?.Invoke();
                    if (reverse) Destroy(gameObject);
                    else Destroy(this);
                }
            }
            private float EaseInOutCubic(float t)
            {
                return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
            }
        }

        // Variables
        // Important
        // Objects
        public static GameObject menu;
        public static GameObject menuBackground;   
        public static GameObject reference;
        public static GameObject canvasObject;
        public static Coroutine animateTitleCoroutine;
        public static GameObject Pointer = null;
        private static Shader _guiTextShader;
        public static ButtonInfo ActiveCategory = null;
        public static Camera _pcCamera;

        public static Color pinwheelColor1 = new Color32(0, 0, 0, byte.MaxValue);

        public static Color pinwheelColor2 = new Color32(128, 0, 128, byte.MaxValue);
        public static Shader GuiTextShader => _guiTextShader ??= Shader.Find("GUI/Text Shader");
        private static Shader _uberShader;
        public static Shader UberShader => _uberShader ??= Shader.Find("GorillaTag/UberShader");
        public static VRRig[] CachedActiveRigs = Array.Empty<VRRig>();

        public static SphereCollider buttonCollider;
        public static Camera TPC;
        public static Text fpsObject;
        public static float IncrementCooldown = 0f;
        

        private static GameObject GunPointer;
        private static LineRenderer GunLine;

        // Data
        public static int pageNumber = 0;
        public static int _currentCategory;
        public static int currentCategory
        {
            get => _currentCategory;
            set
            {
                _currentCategory = value;
                pageNumber = 0;
            }
        }
    }
}

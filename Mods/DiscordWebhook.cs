using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Vaga.Components;

namespace Vaga.Mods
{
    /// <summary>
    /// Fire-and-forget Discord webhook sender for PlayerTracker events.
    /// Set WebhookUrl from menu or hardcode before build.
    /// </summary>
    internal static class DiscordWebhook
    {
        // ============================================================
        // PASTE YOUR DISCORD WEBHOOK URL HERE BEFORE BUILDING
        // Example: "https://discord.com/api/webhooks/ID/TOKEN"
        // ============================================================
        public static string WebhookUrl = "";

        public static bool Enabled = true;
        public static bool SendJoins = true;
        public static bool SendLeaves = true;
        public static bool SendKnownOnly = true;   // if true, only known player events
        public static bool SendHopFound = true;
        public static bool SendHopStatus = false;  // hop leave/join noise
        public static bool SendScans = false;

        public static void SetUrl(string url)
        {
            WebhookUrl = (url ?? "").Trim();
            Debug.Log("[DiscordWebhook] URL set: " + (string.IsNullOrEmpty(WebhookUrl) ? "(empty)" : "ok"));
        }

        public static bool IsConfigured =>
            Enabled && !string.IsNullOrEmpty(WebhookUrl) && WebhookUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase);

        public static void Send(string content)
        {
            if (!IsConfigured || string.IsNullOrEmpty(content)) return;
            try
            {
                if (CoroutineHandler.Instance != null)
                    ((MonoBehaviour)CoroutineHandler.Instance).StartCoroutine(Post(content));
                else
                    Debug.LogWarning("[DiscordWebhook] No CoroutineHandler — cannot send.");
            }
            catch (Exception e)
            {
                Debug.LogError("[DiscordWebhook] Send failed: " + e.Message);
            }
        }

        public static void SendEmbed(string title, string description, int color)
        {
            if (!IsConfigured) return;
            try
            {
                // Minimal embed JSON (no external JSON lib required)
                string safeTitle = Escape(title);
                string safeDesc = Escape(description);
                string json =
                    "{\"embeds\":[{" +
                    "\"title\":\"" + safeTitle + "\"," +
                    "\"description\":\"" + safeDesc + "\"," +
                    "\"color\":" + color +
                    "}]}";

                if (CoroutineHandler.Instance != null)
                    ((MonoBehaviour)CoroutineHandler.Instance).StartCoroutine(PostRaw(json));
            }
            catch (Exception e)
            {
                Debug.LogError("[DiscordWebhook] Embed failed: " + e.Message);
            }
        }

        public static void NotifyKnown(string display, string uid, string room, string eventType)
        {
            if (!IsConfigured) return;
            if (SendKnownOnly == false && eventType != "KNOWN" && eventType != "HOP FOUND") { /* still allow if configured */ }

            int color = eventType.Contains("LEAVE") ? 15158332 : // red-ish
                        eventType.Contains("FOUND") || eventType.Contains("KNOWN") ? 16776960 : // yellow
                        5763719; // green

            string desc =
                "**Name:** " + display + "\\n" +
                "**UserId:** `" + uid + "`\\n" +
                "**Room:** `" + (room ?? "?") + "`\\n" +
                "**Event:** " + eventType + "\\n" +
                "**Time:** " + DateTime.UtcNow.ToString("u");

            SendEmbed("PlayerTracker — " + eventType, desc, color);
        }

        public static void NotifyPlain(string text)
        {
            if (!IsConfigured) return;
            Send(text);
        }

        public static void Test()
        {
            if (string.IsNullOrEmpty(WebhookUrl))
            {
                Debug.LogWarning("[DiscordWebhook] No URL set.");
                return;
            }
            SendEmbed("PlayerTracker — Test", "Webhook connected.\\nTime: " + DateTime.UtcNow.ToString("u"), 3447003);
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        }

        private static IEnumerator Post(string content)
        {
            string json = "{\"content\":\"" + Escape(content) + "\"}";
            yield return PostRaw(json);
        }

        private static IEnumerator PostRaw(string json)
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            using (UnityWebRequest req = new UnityWebRequest(WebhookUrl, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 8;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                    Debug.LogWarning("[DiscordWebhook] HTTP " + req.responseCode + " " + req.error);
                else
                    Debug.Log("[DiscordWebhook] OK " + req.responseCode);
            }
        }
    }
}

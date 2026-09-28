using GorillaLocomotion;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Vaga.Mods.Exploits;
using Random = UnityEngine.Random;

namespace Vaga.Mods
{
    /// <summary>
    /// Full stub implementations — movement/VRRig toggles + one-shot Global/OP/Settings actions.
    /// </summary>
    internal static class MenuStubs
    {
        public static void Nop() { }
        public static void Log(string name) { Debug.Log("[MORPHINE] " + name); }

        // Movement
        public static bool PlatformGun, PlatformSpammer, TriggerFly, NoClipFly, HandFly;
        public static bool SlingshotFly, JoystickFly, ZeroGFly, UpDown, LeftRight, C4, NoClip;
        public static bool SteamLongArms, ReallyLongArms, LegitLongArms, NormalArms;
        public static bool SpeedBoost, IntegratedSpeed, GripSpeed, PullMod, SlideControl;
        public static bool ForceTagFreeze, NoTagFreeze, UncapSpeed, SuperJump, WallWalk, IronMonkey;

        // VRRig
        public static bool GhostMonkey = false, InvisMonkey = false, GrabRig, HelicopterRig, RigMacro;
        public static bool RGBMonkey, StrobeMonkey, FlashColor, StrobeColor, RainbowColor, HardRainbow;
        public static bool SexGun, SpectateGun, ScareGun, CopyMoveGun, CopyPlayerGun, CopyIdGun;
        public static bool PunchMod, SpazHead, SpinHeadX, SpinHeadY, SpinHeadZ, CosmeticBypass;

        // Settings
        public static bool SmoothMenu, GunPointer, GunFade, GunSmooth, GunHaptics, GunEffects;
        public static bool AutoLoadConfig, OpenAnim, ButtonAnim;
        public static float AnimSpeed = 1f, GunWidth = 0.02f, GunSpeed = 1f, GunSize = 1f;
        public static int Theme, MenuButton, GunStyle, GunColor, GunShootSound, GunTriggerSound;

        private static float _hue;
        private static float _spin;
        private static LineRenderer _gunLr;
        private static VRRig _locked;
        private static Vector3 _archivePos;

        public static void TickMovement()
        {
            // Fast path: skip entire body if no movement/vrrig flags
            if (!(PlatformGun || PlatformSpammer || TriggerFly || NoClipFly || HandFly ||
                  SlingshotFly || JoystickFly || ZeroGFly || UpDown || LeftRight || C4 || NoClip ||
                  SteamLongArms || ReallyLongArms || LegitLongArms || NormalArms ||
                  SpeedBoost || IntegratedSpeed || GripSpeed || PullMod || SlideControl ||
                  ForceTagFreeze || NoTagFreeze || UncapSpeed || SuperJump || WallWalk || IronMonkey ||
                  GhostMonkey || InvisMonkey || GrabRig || HelicopterRig || RigMacro ||
                  RGBMonkey || StrobeMonkey || FlashColor || StrobeColor || RainbowColor || HardRainbow ||
                  SexGun || SpectateGun || ScareGun || CopyMoveGun || CopyPlayerGun || CopyIdGun ||
                  PunchMod || SpazHead || SpinHeadX || SpinHeadY || SpinHeadZ || CosmeticBypass))
                return;

            try
            {
                var tagger = GorillaTagger.Instance;
                if (tagger == null || tagger.offlineVRRig == null) return;
                var rig = tagger.offlineVRRig;
                var player = GTPlayer.Instance;

                // --- movement ---
                if (TriggerFly || HandFly || SlingshotFly || JoystickFly || ZeroGFly || NoClipFly)
                    ApplyFly(player, rig);
                if (UpDown) ApplyAxis(player, Vector3.up);
                if (LeftRight) ApplyAxis(player, Vector3.right);
                if (NoClip) SetNoClip(player, true);
                if (SpeedBoost || IntegratedSpeed || GripSpeed || UncapSpeed) ApplySpeed(player);
                if (PullMod && player != null) player.transform.position += Vector3.down * 0.02f;
                if (SuperJump && player != null && ControllerInputPoller.instance.rightControllerPrimaryButton)
                    player.transform.position += Vector3.up * 0.25f;
                if (WallWalk) TryWallWalk(player);
                if (IronMonkey && player != null) player.transform.position += Vector3.down * 0.05f;
                if (C4) ApplyC4(player);
                if (PlatformGun || PlatformSpammer) TickPlatformGun();
                ApplyArms(rig);

                if (NoTagFreeze) ClearTagFreeze();
                if (ForceTagFreeze) /* client-side feel only */ { }

                // --- VRRig cosmetics / motion ---
                if (RGBMonkey || RainbowColor || HardRainbow || FlashColor || StrobeColor || StrobeMonkey)
                    CycleColor(rig);
                if (SpazHead || SpinHeadX || SpinHeadY || SpinHeadZ) SpinHead(rig);
                if (HelicopterRig) Helicopter(rig);
                if (GrabRig) GrabSelf(rig);
                if (RigMacro) /* pulse */ rig.transform.position += Random.insideUnitSphere * 0.01f;
                if (PunchMod) PunchNearby();
                if (SexGun || SpectateGun || ScareGun || CopyMoveGun || CopyPlayerGun || CopyIdGun)
                    TickVrrigGuns();
                if (CosmeticBypass) /* no-op visual flag */ { }
            }
            catch { }
        }

        // ========== one-shot / settings ==========
        public static void ThemeCycle() { Theme = (Theme + 1) % 8; Log("Theme=" + Theme); }
        public static void MenuButtonCycle() { MenuButton = (MenuButton + 1) % 4; Log("MenuButton=" + MenuButton); }
        public static void GunStyleCycle() { GunStyle = (GunStyle + 1) % 5; Log("GunStyle=" + GunStyle); }
        public static void GunColorCycle() { GunColor = (GunColor + 1) % 12; Log("GunColor=" + GunColor); }
        public static void GunWidthCycle() { GunWidth = GunWidth >= 0.05f ? 0.01f : GunWidth + 0.01f; Log("GunWidth=" + GunWidth); }
        public static void GunSpeedCycle() { GunSpeed = GunSpeed >= 3f ? 0.5f : GunSpeed + 0.5f; Log("GunSpeed=" + GunSpeed); }
        public static void GunSizeCycle() { GunSize = GunSize >= 2f ? 0.5f : GunSize + 0.25f; Log("GunSize=" + GunSize); }
        public static void GunShootCycle() { GunShootSound = (GunShootSound + 1) % 6; Log("GunShoot=" + GunShootSound); }
        public static void GunTrigCycle() { GunTriggerSound = (GunTriggerSound + 1) % 6; Log("GunTrig=" + GunTriggerSound); }
        public static void AnimSpeedCycle() { AnimSpeed = AnimSpeed >= 2f ? 0.5f : AnimSpeed + 0.25f; Log("AnimSpeed=" + AnimSpeed); }
        public static void SaveConfig() { Log("Save Config"); PlayerPrefs.Save(); }
        public static void LoadConfig() { Log("Load Config"); }
        public static void SoundBoardRefresh() { Log("SoundBoard Refresh"); }
        public static void LoopSounds() { Log("Loop Sounds"); }
        public static void TestNotification() { Log("Test Notification"); }
        public static void Notifs() { Log("Notifs"); }

        // Global fun one-shots routed into modules where possible
        public static void StumpKickAll() { ModeKicks.KickCasual(); }
        public static void StumpKickGun() { /* use KickGun */ KickGun.Enabled = true; }
        public static void HoverboardAnywhere() { GlobalFun.BoardRain = true; }
        public static void RGBHoverboard() { GlobalFun.BoardRain = true; }
        public static void HoverboardBlink() { GlobalFun.BoardSwarm = true; }
        public static void HoverboardOrbit() { GlobalFun.BoardSwarm = true; }
        public static void HoverboardSpaz() { GlobalFun.BoardSwarm = true; }
        public static void HoverboardSpeedBoost() { GlobalFun.BoardRain = true; }
        public static void HoverboardMoonJump() { GlobalFun.SuperSwim = true; }
        public static void BoardShooter() { GlobalFun.ProjectileLaunchOnce(); }
        public static void BoardTrail() { GlobalFun.BoardRain = true; }
        public static void BoardDropGun() { GlobalFun.BoardSwarm = true; }
        public static void GiveBoardGun() { GlobalFun.BoardRain = true; }
        public static void SplashHands() { GlobalFun.SplashAura = true; }
        public static void SplashTrail() { GlobalFun.SplashAura = true; }
        public static void SplashGeyser() { GlobalFun.SplashAllPlayers(); }
        public static void SplashSpiral() { GlobalFun.SplashAura = true; }
        public static void SplashRain() { GlobalFun.SplashAllPlayers(); }
        public static void SplashPlayerGun() { GlobalFun.SplashGun = true; }
        public static void VoiceFeedback() { Log("Voice Feedback"); }
        public static void ChipmunkVoice() { Log("Chipmunk Voice"); }
        public static void RobotVoice() { Log("Robot Voice"); }
        public static void RadioVoice() { Log("Radio Voice"); }
        public static void OldMicVoice() { Log("Old Mic Voice"); }
        public static void EchoVoice() { Log("Echo Voice"); }
        public static void StutterVoice() { Log("Stutter Voice"); }
        public static void RopeGunG() { RopeControl.RopeGunEnabled = true; }
        public static void RopeSpazG() { RopeControl.RopeSpazEnabled = true; }
        public static void RopeHelicopter() { RopeControl.RopeUpEnabled = true; }
        public static void RopeTornado() { RopeControl.RopeSpazEnabled = true; }
        public static void RopeFreeze() { RopeControl.RopeDownEnabled = true; }
        public static void BlockGrabGun() { RaiseBlock(1); }
        public static void BlockLauncherGun() { RaiseBlock(2); }
        public static void BlockYeet() { RaiseBlock(3); }
        public static void StealBlocksGun() { RaiseBlock(4); }
        public static void RecycleGun() { RaiseBlock(5); }
        public static void RecycleAllBlocks() { RaiseBlock(6); }
        public static void BlockNuke() { RaiseBlock(7); }
        public static void BlockTornado() { RaiseBlock(8); }
        public static void ConveyorSpam() { RaiseBlock(9); }
        public static void VimUnmuteGun() { GlobalFun.VimMuteGun = false; }
        public static void VimUntimeoutGun() { GlobalFun.VimTimeoutGun = false; }

        // OP extras
        public static void LagSpikeAll() { foreach (var p in PhotonNetwork.PlayerListOthers) LagBurst(p); }
        public static void HardLagAll() { LagSpikeAll(); }
        public static void WeakLagAll() { LagSpikeAll(); }
        public static void DestroyAll() { NetworkOddities.DestroySpamEnabled = true; }
        public static void DestroyOthers() { NetworkOddities.DestroySpamEnabled = true; }
        public static void DestroyGun() { NetworkOddities.DestroySpamEnabled = true; }
        public static void DestroyAura() { NetworkOddities.DestroySpamEnabled = true; }
        public static void DeafenAll() { GlobalFun.VimMuteAll(); }
        public static void DeafenOthers() { GlobalFun.VimMuteAll(); }
        public static void DeafenGun() { GlobalFun.VimMuteGun = true; }
        public static void DeafenAura() { GlobalFun.VimMuteAll(); }
        public static void SpyRoom() { Log("Spy Room"); }
        public static void StumpAntiBan() { try { AntiBanRoom.Create(); } catch { Log("Stump Anti-Ban"); } }
        public static void PartyAntiBan() { try { AntiBanRoom.CreatePrivateNoAC(); } catch { Log("Party Anti-Ban"); } }

        // AB extras
        public static void HardLagGun() { LagGun.Enabled = true; }
        public static void HardLagAura() { HardLagAll(); }
        public static void HardLagWhenTouched() { HardLagAll(); }
        public static void ChangeNameAll()
        {
            try
            {
                foreach (var p in PhotonNetwork.PlayerListOthers)
                    PhotonNetwork.RaiseEvent(204, new object[] { "name", p.ActorNumber, "." },
                        new RaiseEventOptions { TargetActors = new[] { p.ActorNumber } }, SendOptions.SendReliable);
            }
            catch { }
        }
        public static void ChangeNameGun() { ChangeNameAll(); }
        public static void BreakGameMode()
        {
            try
            {
                PhotonNetwork.RaiseEvent(230, new object[] { "breakgm", PhotonNetwork.ServerTimestamp },
                    new RaiseEventOptions { Receivers = ReceiverGroup.All }, SendOptions.SendReliable);
            }
            catch { }
        }
        public static void RevokeGhostOnYourTouch() { GhostVariants.GhostOnYourTouch = false; }

        // ---------- internals ----------
        private static void ApplyFly(GTPlayer player, VRRig rig)
        {
            if (player == null) return;
            Transform hand = rig.rightHandTransform;
            Vector3 dir = hand != null ? hand.forward : player.transform.forward;
            float speed = (SlingshotFly ? 0.35f : 0.18f) * (ZeroGFly ? 0.6f : 1f);
            if (ControllerInputPoller.instance.rightControllerPrimaryButton ||
                ControllerInputPoller.instance.rightControllerIndexFloat > 0.5f ||
                TriggerFly || JoystickFly || HandFly || NoClipFly)
                player.transform.position += dir * speed;
            if (ZeroGFly) player.transform.position += Vector3.up * 0.02f;
        }

        private static void ApplyAxis(GTPlayer player, Vector3 axis)
        {
            if (player == null) return;
            float v = 0f;
            try { v = ControllerInputPoller.instance.rightControllerPrimaryButton ? 1f : (ControllerInputPoller.instance.leftControllerPrimaryButton ? -1f : 0f); } catch { }
            player.transform.position += axis * v * 0.15f;
        }

        private static void SetNoClip(GTPlayer player, bool on)
        {
            if (player == null) return;
            try
            {
                foreach (var c in player.GetComponentsInChildren<Collider>())
                    if (c != null) c.enabled = !on;
            }
            catch { }
        }

        private static void ApplySpeed(GTPlayer player)
        {
            if (player == null) return;
            float m = SpeedBoost || IntegratedSpeed ? 1.35f : 1.15f;
            if (GripSpeed && ControllerInputPoller.instance.rightGrab) m = 1.6f;
            if (UncapSpeed) m = 2f;
            try { player.transform.position += player.transform.forward * 0.05f * m; } catch { }
        }

        private static void TryWallWalk(GTPlayer player)
        {
            if (player == null) return;
            if (Physics.Raycast(player.transform.position, player.transform.forward, out var hit, 0.6f))
                player.transform.position += Vector3.up * 0.08f;
        }

        private static void ApplyC4(GTPlayer player)
        {
            if (player == null) return;
            if (ControllerInputPoller.instance.rightControllerPrimaryButton)
                player.transform.position += Random.insideUnitSphere * 0.4f + Vector3.up * 0.5f;
        }

        private static void ApplyArms(VRRig rig)
        {
            float scale = 1f;
            if (ReallyLongArms) scale = 2.2f;
            else if (SteamLongArms) scale = 1.6f;
            else if (LegitLongArms) scale = 1.25f;
            else if (NormalArms) scale = 1f;
            else return;
            try
            {
                if (rig.leftHandTransform != null) rig.leftHandTransform.localScale = Vector3.one * scale;
                if (rig.rightHandTransform != null) rig.rightHandTransform.localScale = Vector3.one * scale;
            }
            catch { }
        }

        private static void ClearTagFreeze()
        {
            try
            {
                // best-effort: zero freeze timers via locomotion if exposed
                var player = GTPlayer.Instance;
                if (player != null) { /* tag freeze is mode-side; client motion continues */ }
            }
            catch { }
        }

        private static void CycleColor(VRRig rig)
        {
            _hue = (_hue + Time.deltaTime * (HardRainbow ? 1.2f : 0.35f)) % 1f;
            Color c = Color.HSVToRGB(_hue, StrobeColor || StrobeMonkey ? (Time.frameCount % 10 < 5 ? 1f : 0.2f) : 0.85f, 1f);
            if (FlashColor && Time.frameCount % 8 < 2) c = Color.white;
            try
            {
                foreach (var r in rig.GetComponentsInChildren<Renderer>())
                    if (r != null && r.material != null) r.material.color = c;
            }
            catch { }
        }

        private static void SpinHead(VRRig rig)
        {
            _spin += Time.deltaTime * 360f;
            try
            {
                Vector3 e = rig.transform.localEulerAngles;
                if (SpinHeadX || SpazHead) e.x = _spin;
                if (SpinHeadY || SpazHead) e.y = _spin;
                if (SpinHeadZ || SpazHead) e.z = _spin;
                rig.transform.localEulerAngles = e;
            }
            catch { }
        }

        private static void Helicopter(VRRig rig)
        {
            try { rig.transform.Rotate(Vector3.up, 20f, Space.Self); rig.transform.position += Vector3.up * 0.05f; } catch { }
        }

        private static void GrabSelf(VRRig rig)
        {
            try
            {
                Transform hand = rig.rightHandTransform;
                if (hand != null) rig.transform.position = hand.position;
            }
            catch { }
        }

        private static void PunchNearby()
        {
            Vector3 me = GorillaTagger.Instance.offlineVRRig.transform.position;
            foreach (var r in Vaga.Mods.LeakGuard.Rigs())
            {
                if (r == null || r == GorillaTagger.Instance.offlineVRRig) continue;
                if (Vector3.Distance(me, r.transform.position) < 1.5f)
                {
                    try
                    {
                        var view = r.GetComponent<PhotonView>();
                        if (view != null) view.TransferOwnership(PhotonNetwork.LocalPlayer);
                        r.transform.position += (r.transform.position - me).normalized * 0.4f + Vector3.up * 0.2f;
                    }
                    catch { }
                }
            }
        }

        private static void TickVrrigGuns()
        {
            EnsureGunLr();
            Transform hand = GorillaTagger.Instance.offlineVRRig.rightHandTransform;
            Vector3 o = hand.position, d = hand.forward;
            _gunLr.enabled = true;
            _gunLr.SetPosition(0, o);
            if (!Physics.Raycast(new Ray(o, d), out var hit, 100f))
            {
                _gunLr.SetPosition(1, o + d * 100f);
                return;
            }
            _gunLr.SetPosition(1, hit.point);
            if (ControllerInputPoller.instance.rightControllerIndexFloat < 0.55f) return;
            VRRig target = hit.collider.GetComponentInParent<VRRig>();
            if (target == null)
            {
                foreach (var c in Physics.OverlapSphere(hit.point, 0.5f))
                {
                    target = c.GetComponentInParent<VRRig>();
                    if (target != null && target != GorillaTagger.Instance.offlineVRRig) break;
                }
            }
            if (target == null) return;
            if (SpectateGun) GorillaTagger.Instance.offlineVRRig.transform.position = target.transform.position + Vector3.up * 1.5f;
            if (ScareGun) target.transform.position += Vector3.up * 0.5f + Random.insideUnitSphere;
            if (CopyMoveGun || CopyPlayerGun) GorillaTagger.Instance.offlineVRRig.transform.rotation = target.transform.rotation;
            if (CopyIdGun) try { PhotonNetwork.NickName = Vaga.Mods.NetPlayerUtil.Nick(target) ?? PhotonNetwork.NickName; } catch { }
            if (SexGun) target.transform.position = hand.position + hand.forward * 0.5f;
        }

        private static void TickPlatformGun()
        {
            if (ControllerInputPoller.instance.rightControllerIndexFloat < 0.5f && !PlatformSpammer) return;
            try
            {
                Transform hand = GorillaTagger.Instance.offlineVRRig.rightHandTransform;
                Vector3 pos = hand.position + hand.forward * 1.2f;
                PhotonNetwork.RaiseEvent(202, new object[] { "platform", pos, Quaternion.identity, PhotonNetwork.LocalPlayer.ActorNumber },
                    new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendUnreliable);
            }
            catch { }
        }

        private static void RaiseBlock(int mode)
        {
            try
            {
                PhotonNetwork.RaiseEvent(227, new object[] { mode, GorillaTagger.Instance.offlineVRRig.transform.position, Random.insideUnitSphere },
                    new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendUnreliable);
            }
            catch { }
        }

        private static void LagBurst(Player p)
        {
            if (p == null) return;
            var opt = new RaiseEventOptions { TargetActors = new[] { p.ActorNumber } };
            byte[] pad = new byte[2048];
            for (int i = 0; i < 10; i++)
            {
                try { PhotonNetwork.RaiseEvent((byte)(200 + i % 5), new object[] { pad, p.ActorNumber }, opt, SendOptions.SendUnreliable); }
                catch { }
            }
        }

        private static void EnsureGunLr()
        {
            if (_gunLr != null) return;
            var go = new GameObject("MenuStubsGunLR");
            _gunLr = go.AddComponent<LineRenderer>();
            _gunLr.startWidth = 0.012f; _gunLr.endWidth = 0.012f; _gunLr.positionCount = 2;
            _gunLr.material = Vaga.Mods.LeakGuard.LineMaterial();
            _gunLr.startColor = Color.white; _gunLr.endColor = Color.gray;
        }
    }
}

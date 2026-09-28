using System;
using System.Collections.Generic;
using UnityEngine;
using Vaga.Mods.Exploits;

namespace Vaga.Mods
{
    /// <summary>
    /// Central tick registry — avoids SelectMany over 380 buttons every frame.
    /// Modules register once; Main only invokes this list.
    /// </summary>
    public static class ModRegistry
    {
        private static readonly List<Action> _ticks = new List<Action>(64);
        private static bool _built;

        public static void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            // Always-safe lightweight modules; each checks its own Enabled flags
            _ticks.Add(MenuStubs.TickMovement);
            _ticks.Add(MemoryMonitor.Update);
            _ticks.Add(DesktopGui.Update);
            _ticks.Add(VoiceStabilizer.Update);
            _ticks.Add(FriendTools.Update);
            _ticks.Add(TentacleGun.Update);
            _ticks.Add(PcClick.Update);
            _ticks.Add(SpoofEverything.Update);
            _ticks.Add(StumpKickFix.Update);
            _ticks.Add(MaterialOrbit.Update);
            _ticks.Add(GrabFling.Update);
            _ticks.Add(BarrelFlingGun.Update);
            _ticks.Add(GearDraw.Update);
            _ticks.Add(KickToGamemode.Update);
            _ticks.Add(TagBotLoop.Update);
            _ticks.Add(AntiBanGhostOps.Update);
            _ticks.Add(TagGuardian.Update);
            _ticks.Add(VisualESP.Update);
            _ticks.Add(GlobalFun.Update);
            _ticks.Add(RopeControl.Update);
            _ticks.Add(IdentitySafety.Update);
            _ticks.Add(NetworkOddities.Update);
            _ticks.Add(AntiReport.Update);
            _ticks.Add(GhostVariants.Update);
            _ticks.Add(MasterSI.Update);
            _ticks.Add(KickGunUpdate);
            _ticks.Add(LagGunUpdate);
            _ticks.Add(FreezeGunUpdate);
            _ticks.Add(CrashSuiteUpdate);
            _ticks.Add(VoiceGunUpdate);
            _ticks.Add(BanGunUpdate);
            _ticks.Add(ServerToolsUpdate);
            _ticks.Add(AntiBanUpdate);
        }

        public static void TickAll()
        {
            EnsureBuilt();
            for (int i = 0; i < _ticks.Count; i++)
            {
                try { _ticks[i]?.Invoke(); }
                catch (Exception ex) { Debug.LogError("[MORPHINE] tick: " + ex.Message); }
            }
        }

        // Thin wrappers so static Enabled checks stay in module
        private static void KickGunUpdate() { if (KickGun.Enabled) KickGun.Update(); }
        private static void LagGunUpdate() { if (LagGun.Enabled) LagGun.Update(); }
        private static void FreezeGunUpdate() { if (FreezeGun.Enabled) FreezeGun.Update(); }
        private static void CrashSuiteUpdate()
        {
            if (CrashGun.Enabled) CrashGun.Update();
            if (CrashGunInstant.Enabled) CrashGunInstant.Update();
            if (CrashGunStealth.Enabled) CrashGunStealth.Update();
            if (CrashGunClassic.Enabled) CrashGunClassic.Update();
            if (CrashGunSilent.Enabled) CrashGunSilent.Update();
            if (CrashGunOwnership.Enabled) CrashGunOwnership.Update();
            if (CrashGunNuke.Enabled) CrashGunNuke.Update();
            if (CrashGunAntiBan.Enabled) CrashGunAntiBan.Update();
        }
        private static void VoiceGunUpdate()
        {
            try { VoiceGun.Update(); } catch { }
        }
        private static void BanGunUpdate()
        {
            if (BanGun.Enabled) BanGun.Update();
        }
        private static void ServerToolsUpdate()
        {
            try { if (ServerCrasher.Enabled) ServerCrasher.Update(); } catch { }
            try { if (ServerOverload.Enabled) ServerOverload.Update(); } catch { }
            try { if (JoinPingCrasher.Enabled) JoinPingCrasher.Update(); } catch { }
        }
        private static void AntiBanUpdate()
        {
            try { AntiBan.Update(); } catch { }
            try { if (AntiBanRoom.Enabled) AntiBanRoom.Update(); } catch { }
            try { AntiMute.Update(); } catch { }
        }
    }
}

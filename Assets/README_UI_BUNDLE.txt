MORPHINE UI AssetBundle
=======================

Place a Unity AssetBundle named: morphine_ui

Search paths (first hit wins):
  BepInEx/plugins/MORPHINE/morphine_ui
  BepInEx/plugins/morphine_ui
  next to MORPHINE.dll / morphine_ui
  next to MORPHINE.dll / Assets/morphine_ui

Optional assets inside the bundle:
  PanelMat      (Material) - menu background
  ButtonMat     (Material) - button off
  ButtonOnMat   (Material) - button on
  AccentMat     (Material) - accent / outline
  TitleFont     (Font)
  BodyFont      (Font)

If the bundle is missing, MORPHINE builds a procedural purple/glass theme at runtime
(URP Lit/Unlit materials + OS fonts). No bundle required to run.

Unity export tip:
  1. Create materials with your look
  2. Name them exactly as above
  3. Build AssetBundle for standalone windows (or your target)
  4. Copy the file to BepInEx/plugins/MORPHINE/morphine_ui (no extension needed)

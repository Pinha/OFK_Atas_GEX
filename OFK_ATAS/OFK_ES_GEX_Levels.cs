// ============================================================================
//  OFK_ES_GEX_Levels.cs — ATAS
//  Reads full_levels_ES.json and displays all GEX + Options levels.
//
//  Chart levels: Gamma Flip, Vol Trigger, Call Wall, Put Wall, Risk Pivot,
//                Vanna Flip, Charm Magnet, Max Pain, EM High, EM Low,
//                Top OI #1, Top OI #2, Top OI #3
//
//  Panel: GEX LEVELS (run_morning_ES.py) + Briefing (opens PDF)
// ============================================================================
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Control;
using OFT.Rendering.Tools;
using DrawingColor = System.Drawing.Color;

namespace OFK_GEX
{
    [DisplayName("OFK ES GEX Levels")]
    [Category("OFK Suite")]
    [Description("Greeks Options levels ES. Reads full_levels_ES.json.")]
    public class OFK_ES_GEX_Levels : Indicator
    {
        #region Snapshot

        // GexSnapshot, MetaSnapshot and GexLoader are defined in OFK_GexShared.cs
        // (shared with OFK_ES_GEX_Levels and OFK_*_ContextScore).

        // JSON schema version expected by this indicator (synced with config.py)
        private const string EXPECTED_JSON_SCHEMA_VERSION = "1.0";

        #endregion
        #region 01.Source

        [Display(Name = "JSON Path", GroupName = "01.Source", Order = 1)]
        public string JsonPath { get; set; } = Path.Combine(OfkEnv.Data, "full_levels_ES.json");

        [Display(Name = "Refresh (minutes)", GroupName = "01.Source", Order = 2)]
        [Range(1, 240)]
        public int RefreshMinutes { get; set; } = 30;

        #endregion

        #region 02.GEX Levels

        [Display(Name = "Gamma Flip",            GroupName = "02.GEX Levels", Order = 1)]
        public bool ShowGammaFlip   { get; set; } = true;
        [Display(Name = "Vol Trigger",           GroupName = "02.GEX Levels", Order = 2)]
        public bool ShowVolTrigger  { get; set; } = true;
        [Display(Name = "Call Wall",             GroupName = "02.GEX Levels", Order = 3)]
        public bool ShowCallWall    { get; set; } = true;
        [Display(Name = "Put Wall",              GroupName = "02.GEX Levels", Order = 4)]
        public bool ShowPutWall     { get; set; } = true;
        [Display(Name = "Risk Pivot (trapdoor)", GroupName = "02.GEX Levels", Order = 5)]
        public bool ShowRiskPivot   { get; set; } = true;

        #endregion

        #region 02b.INTRADAY Levels (CBOE 0-7 DTE)

        [Display(Name = "Call Wall intraday",    GroupName = "02b.INTRADAY Levels (0-7d)", Order = 1)]
        public bool ShowCallWallIntraday  { get; set; } = true;
        [Display(Name = "Put Wall intraday",     GroupName = "02b.INTRADAY Levels (0-7d)", Order = 2)]
        public bool ShowPutWallIntraday   { get; set; } = true;
        [Display(Name = "Top OI intraday #1",    GroupName = "02b.INTRADAY Levels (0-7d)", Order = 3)]
        public bool ShowTopOIIntraday1    { get; set; } = true;
        [Display(Name = "Top OI intraday #2",    GroupName = "02b.INTRADAY Levels (0-7d)", Order = 4)]
        public bool ShowTopOIIntraday2    { get; set; } = true;
        [Display(Name = "Top OI intraday #3",    GroupName = "02b.INTRADAY Levels (0-7d)", Order = 5)]
        public bool ShowTopOIIntraday3    { get; set; } = true;
        [Display(Name = "cTrans (call dom.)",    GroupName = "02b.INTRADAY Levels (0-7d)", Order = 6,
                 Description = "Level above which call gamma dominates (TanukiTrade-style)")]
        public bool ShowCTransIntraday    { get; set; } = true;
        [Display(Name = "pTrans (put dom.)",     GroupName = "02b.INTRADAY Levels (0-7d)", Order = 7,
                 Description = "Level below which put gamma dominates (TanukiTrade-style)")]
        public bool ShowPTransIntraday    { get; set; } = true;
        [Display(Name = "D+ (delta+ max)",       GroupName = "02b.INTRADAY Levels (0-7d)", Order = 8,
                 Description = "Max positive DEX — strike where dealers buy aggressively (bullish hedging)")]
        public bool ShowDexPlusIntraday   { get; set; } = true;
        [Display(Name = "D- (delta- max)",       GroupName = "02b.INTRADAY Levels (0-7d)", Order = 9,
                 Description = "Max negative DEX — strike where dealers sell aggressively (bearish hedging)")]
        public bool ShowDexMinusIntraday  { get; set; } = true;
        [Display(Name = "Abs GEX Ab1",           GroupName = "02b.INTRADAY Levels (0-7d)", Order = 10,
                 Description = "Strike with max absolute gamma (call+put) — strong pin risk")]
        public bool ShowAbsGex1           { get; set; } = true;
        [Display(Name = "Abs GEX Ab2",           GroupName = "02b.INTRADAY Levels (0-7d)", Order = 11)]
        public bool ShowAbsGex2           { get; set; } = true;
        [Display(Name = "Abs GEX Ab3",           GroupName = "02b.INTRADAY Levels (0-7d)", Order = 12)]
        public bool ShowAbsGex3           { get; set; } = true;
        [Display(Name = "GEX Ext #7",            GroupName = "02b.INTRADAY Levels (0-7d)", Order = 13,
                 Description = "Additional wall after main CW/PW (TanukiTrade GEX7)")]
        public bool ShowGexExt1           { get; set; } = true;
        [Display(Name = "GEX Ext #8",            GroupName = "02b.INTRADAY Levels (0-7d)", Order = 14)]
        public bool ShowGexExt2           { get; set; } = true;
        [Display(Name = "GEX Ext #9",            GroupName = "02b.INTRADAY Levels (0-7d)", Order = 15)]
        public bool ShowGexExt3           { get; set; } = true;
        [Display(Name = "GEX Ext #10",           GroupName = "02b.INTRADAY Levels (0-7d)", Order = 16)]
        public bool ShowGexExt4           { get; set; } = true;

        #endregion

        #region 03.VEX/CEX Levels

        [Display(Name = "Vanna Flip",                      GroupName = "03.VEX/CEX Levels", Order = 1)]
        public bool ShowVannaFlip   { get; set; } = true;
        [Display(Name = "Charm Magnet (CME 49d)",          GroupName = "03.VEX/CEX Levels", Order = 2)]
        public bool ShowCharmMagnet { get; set; } = true;

        #endregion

        #region 03b.0DTE Levels (CBOE same-day expirations)

        [Display(Name = "Max Pain 0DTE",      GroupName = "03b.0DTE Levels", Order = 1)]
        public bool ShowMaxPain0DTE     { get; set; } = true;
        [Display(Name = "Pin Strike 0DTE",    GroupName = "03b.0DTE Levels", Order = 2)]
        public bool ShowPinStrike0DTE   { get; set; } = true;
        [Display(Name = "Charm Magnet 0DTE",  GroupName = "03b.0DTE Levels", Order = 3)]
        public bool ShowCharmMagnet0DTE { get; set; } = true;

        #endregion

        #region 04.Options Levels

        [Display(Name = "Max Pain",           GroupName = "04.Options Levels", Order = 1)]
        public bool ShowMaxPain          { get; set; } = true;
        [Display(Name = "Expected Move High", GroupName = "04.Options Levels", Order = 2)]
        public bool ShowExpectedMoveHigh  { get; set; } = true;
        [Display(Name = "Expected Move Low",  GroupName = "04.Options Levels", Order = 3)]
        public bool ShowExpectedMoveLow   { get; set; } = true;
        [Display(Name = "EM band (fill)",     GroupName = "04.Options Levels", Order = 4,
                 Description = "Expected Move band (TastyTrade-style, weighted straddle/strangle formula if available)")]
        public bool ShowEMZone            { get; set; } = false;
        [Display(Name = "EM band opacity %",  GroupName = "04.Options Levels", Order = 5)]
        [Range(2, 30)]
        public int  EMBandOpacity         { get; set; } = 8;
        [Display(Name = "EM band color",      GroupName = "04.Options Levels", Order = 6)]
        public DrawingColor EMBandColor   { get; set; } = DrawingColor.FromArgb(255, 165, 130, 90);

        #endregion

        #region 05.Top OI Levels

        // Disabled by default: structural Top OI (all expirations) are
        // often >5% from spot. Prefer the intraday Top OI (section 02b).
        [Display(Name = "Top OI #1 structural", GroupName = "05.Top OI Levels", Order = 1)]
        public bool ShowTopOI1 { get; set; } = true;
        [Display(Name = "Top OI #2 structural", GroupName = "05.Top OI Levels", Order = 2)]
        public bool ShowTopOI2 { get; set; } = true;
        [Display(Name = "Top OI #3 structural", GroupName = "05.Top OI Levels", Order = 3)]
        public bool ShowTopOI3 { get; set; } = true;

        #endregion

        #region 06.Visual

        [Display(Name = "Pinning zone Call/Put Wall", GroupName = "06.Visual", Order = 1)]
        public bool ShowPinZone   { get; set; } = false;
        [Display(Name = "Line thickness",             GroupName = "06.Visual", Order = 2)]
        [Range(1, 5)]
        public int  LineWidth     { get; set; } = 2;
        [Display(Name = "Label font size",            GroupName = "06.Visual", Order = 3)]
        [Range(7, 14)]
        public int  LabelFontSize { get; set; } = 9;
        [Display(Name = "Pinning zone opacity %",     GroupName = "06.Visual", Order = 4)]
        [Range(1, 40)]
        public int  PinZoneOpacity { get; set; } = 8;

        #endregion

        #region 06b.Gamma Zones (TanukiTrade-style)

        [Display(Name = "Show gamma zones",           GroupName = "06b.Gamma Zones", Order = 1,
                 Description = "Colored zones: positive (above cTrans), transition (between cTrans/pTrans), negative (below pTrans), squeeze (above Call Wall / below Put Wall).")]
        public bool ShowGammaZones { get; set; } = false;

        [Display(Name = "Zone opacity %",             GroupName = "06b.Gamma Zones", Order = 2)]
        [Range(2, 30)]
        public int GammaZoneOpacity { get; set; } = 7;

        [Display(Name = "Positive gamma color",       GroupName = "06b.Gamma Zones", Order = 3)]
        public DrawingColor ZonePositiveColor   { get; set; } = DrawingColor.FromArgb(255, 80, 200, 120);
        [Display(Name = "Transition color",           GroupName = "06b.Gamma Zones", Order = 4)]
        public DrawingColor ZoneTransitionColor { get; set; } = DrawingColor.FromArgb(255, 140, 140, 160);
        [Display(Name = "Negative gamma color",       GroupName = "06b.Gamma Zones", Order = 5)]
        public DrawingColor ZoneNegativeColor   { get; set; } = DrawingColor.FromArgb(255, 220, 80, 80);
        [Display(Name = "Squeeze color (yellow)",     GroupName = "06b.Gamma Zones", Order = 6)]
        public DrawingColor ZoneSqueezeColor    { get; set; } = DrawingColor.FromArgb(255, 240, 220, 60);

        #endregion

        #region 06c.Display fine-tuning (TanukiTrade)

        [Display(Name = "Show level labels", GroupName = "06c.Display fine-tuning", Order = 1,
                 Description = "If OFF, draws lines without any label (clean chart).")]
        public bool ShowLineLabels    { get; set; } = true;

        [Display(Name = "Labels on right",         GroupName = "06c.Display fine-tuning", Order = 2,
                 Description = "Label position: left (default) or right of chart.")]
        public bool LabelOnRight      { get; set; } = false;

        [Display(Name = "Solid lines",             GroupName = "06c.Display fine-tuning", Order = 3,
                 Description = "If ON, solid lines instead of dashed.")]
        public bool UseSolidLines     { get; set; } = false;

        [Display(Name = "Label background opacity %", GroupName = "06c.Display fine-tuning", Order = 4)]
        [Range(20, 100)]
        public int  LabelBgOpacity    { get; set; } = 65;

        [Display(Name = "Right-side extension only", GroupName = "06c.Display fine-tuning", Order = 5,
                 Description = "If ON, lines drawn only from the current bar to the right.")]
        public bool LineExtensionRightOnly { get; set; } = false;

        #endregion

        #region 07.GEX Colors

        [Display(Name = "Gamma Flip",   GroupName = "07.GEX Colors", Order = 1)]
        public DrawingColor GammaFlipColor   { get; set; } = DrawingColor.Yellow;
        [Display(Name = "Vol Trigger",  GroupName = "07.GEX Colors", Order = 2)]
        public DrawingColor VolTriggerColor  { get; set; } = DrawingColor.Gold;
        [Display(Name = "Call Wall",    GroupName = "07.GEX Colors", Order = 3)]
        public DrawingColor CallWallColor    { get; set; } = DrawingColor.LimeGreen;
        [Display(Name = "Put Wall",     GroupName = "07.GEX Colors", Order = 4)]
        public DrawingColor PutWallColor     { get; set; } = DrawingColor.OrangeRed;
        [Display(Name = "Risk Pivot",   GroupName = "07.GEX Colors", Order = 5)]
        public DrawingColor RiskPivotColor   { get; set; } = DrawingColor.Crimson;
        [Display(Name = "Vanna Flip",   GroupName = "07.GEX Colors", Order = 6)]
        public DrawingColor VannaFlipColor   { get; set; } = DrawingColor.Violet;
        [Display(Name = "Charm Magnet", GroupName = "07.GEX Colors", Order = 7)]
        public DrawingColor CharmMagnetColor { get; set; } = DrawingColor.CornflowerBlue;

        #endregion

        #region 08.Options Colors

        [Display(Name = "Max Pain",           GroupName = "08.Options Colors", Order = 1)]
        public DrawingColor MaxPainColor          { get; set; } = DrawingColor.Gray;
        [Display(Name = "Expected Move High", GroupName = "08.Options Colors", Order = 2)]
        public DrawingColor ExpectedMoveHighColor { get; set; } = DrawingColor.MediumAquamarine;
        [Display(Name = "Expected Move Low",  GroupName = "08.Options Colors", Order = 3)]
        public DrawingColor ExpectedMoveLowColor  { get; set; } = DrawingColor.MediumAquamarine;
        [Display(Name = "Top OI #1",          GroupName = "08.Options Colors", Order = 4)]
        public DrawingColor TopOI1Color           { get; set; } = DrawingColor.FromArgb(255, 100, 180, 255);
        [Display(Name = "Top OI #2",          GroupName = "08.Options Colors", Order = 5)]
        public DrawingColor TopOI2Color           { get; set; } = DrawingColor.FromArgb(200, 100, 180, 255);
        [Display(Name = "Top OI #3",          GroupName = "08.Options Colors", Order = 6)]
        public DrawingColor TopOI3Color           { get; set; } = DrawingColor.FromArgb(150, 100, 180, 255);

        #endregion

        #region 08b.INTRADAY Colors

        [Display(Name = "Call Wall intraday", GroupName = "08b.INTRADAY Colors", Order = 1)]
        public DrawingColor CallWallIntradayColor { get; set; } = DrawingColor.FromArgb(255, 50, 255, 100);
        [Display(Name = "Put Wall intraday",  GroupName = "08b.INTRADAY Colors", Order = 2)]
        public DrawingColor PutWallIntradayColor  { get; set; } = DrawingColor.FromArgb(255, 255, 80, 80);
        [Display(Name = "Top OI intraday #1", GroupName = "08b.INTRADAY Colors", Order = 3)]
        public DrawingColor TopOIIntraday1Color   { get; set; } = DrawingColor.FromArgb(255, 255, 220, 100);
        [Display(Name = "Top OI intraday #2", GroupName = "08b.INTRADAY Colors", Order = 4)]
        public DrawingColor TopOIIntraday2Color   { get; set; } = DrawingColor.FromArgb(220, 255, 220, 100);
        [Display(Name = "Top OI intraday #3", GroupName = "08b.INTRADAY Colors", Order = 5)]
        public DrawingColor TopOIIntraday3Color   { get; set; } = DrawingColor.FromArgb(180, 255, 220, 100);
        [Display(Name = "cTrans intraday",    GroupName = "08b.INTRADAY Colors", Order = 6)]
        public DrawingColor CTransIntradayColor   { get; set; } = DrawingColor.FromArgb(255, 120, 220, 140);
        [Display(Name = "pTrans intraday",    GroupName = "08b.INTRADAY Colors", Order = 7)]
        public DrawingColor PTransIntradayColor   { get; set; } = DrawingColor.FromArgb(255, 220, 140, 140);
        [Display(Name = "D+ DEX intraday",    GroupName = "08b.INTRADAY Colors", Order = 8)]
        public DrawingColor DexPlusIntradayColor  { get; set; } = DrawingColor.FromArgb(255, 0, 191, 255);  // DeepSkyBlue, distinct from blue Top OI
        [Display(Name = "D- DEX intraday",    GroupName = "08b.INTRADAY Colors", Order = 9)]
        public DrawingColor DexMinusIntradayColor { get; set; } = DrawingColor.FromArgb(255, 255, 105, 180); // HotPink
        [Display(Name = "Abs GEX Ab1",        GroupName = "08b.INTRADAY Colors", Order = 10)]
        public DrawingColor AbsGex1Color      { get; set; } = DrawingColor.FromArgb(255, 200, 130, 255); // TanukiTrade violet
        [Display(Name = "Abs GEX Ab2",        GroupName = "08b.INTRADAY Colors", Order = 11)]
        public DrawingColor AbsGex2Color      { get; set; } = DrawingColor.FromArgb(220, 200, 130, 255);
        [Display(Name = "Abs GEX Ab3",        GroupName = "08b.INTRADAY Colors", Order = 12)]
        public DrawingColor AbsGex3Color      { get; set; } = DrawingColor.FromArgb(180, 200, 130, 255);
        [Display(Name = "GEX Ext (call side)",GroupName = "08b.INTRADAY Colors", Order = 13,
                 Description = "Extended walls color when call side (net GEX positive)")]
        public DrawingColor GexExtCallColor   { get; set; } = DrawingColor.FromArgb(180, 130, 230, 160);
        [Display(Name = "GEX Ext (put side)", GroupName = "08b.INTRADAY Colors", Order = 14,
                 Description = "Extended walls color when put side (net GEX negative)")]
        public DrawingColor GexExtPutColor    { get; set; } = DrawingColor.FromArgb(180, 230, 130, 130);

        #endregion

        #region 08c.0DTE Colors

        [Display(Name = "Max Pain 0DTE",     GroupName = "08c.0DTE Colors", Order = 1)]
        public DrawingColor MaxPain0DTEColor     { get; set; } = DrawingColor.FromArgb(255, 200, 200, 200);
        [Display(Name = "Pin Strike 0DTE",   GroupName = "08c.0DTE Colors", Order = 2)]
        public DrawingColor PinStrike0DTEColor   { get; set; } = DrawingColor.FromArgb(255, 255, 165, 0);
        [Display(Name = "Charm Magnet 0DTE", GroupName = "08c.0DTE Colors", Order = 3)]
        public DrawingColor CharmMagnet0DTEColor { get; set; } = DrawingColor.FromArgb(255, 200, 100, 255);

        #endregion

        #region 09.Floating Panel

        [Display(Name = "Show panel", GroupName = "09.Floating Panel", Order = 1)]
        public bool ShowPanel { get; set; } = true;

        [Display(Name = "Panel collapsed (click the header)", GroupName = "09.Floating Panel", Order = 10)]
        public bool PanelCollapsed { get; set; } = false;

        [Display(Name = "Panel X (px)", GroupName = "09.Floating Panel", Order = 11)]
        [Range(0, 5000)]
        public int PanelX { get; set; } = 3;

        // Renamed from PanelY: drops the 40 px saved by the previous build (the
        // panel now sits at the very top and shifts ATAS' own overlays aside).
        [Display(Name = "Panel Y (px)", GroupName = "09.Floating Panel", Order = 12)]
        [Range(0, 5000)]
        public int PanelTop { get; set; } = 3;

        [Display(Name = "Panel font size", GroupName = "09.Floating Panel", Order = 13)]
        [Range(7, 16)]
        public int PanelFontSize { get; set; } = 9;

        [Display(Name = "Panel opacity (%)", GroupName = "09.Floating Panel", Order = 14)]
        [Range(20, 100)]
        public int PanelOpacity { get; set; } = 90;

        [Display(Name = "Python path (exe)", GroupName = "09.Floating Panel", Order = 2)]
        public string PythonExePath { get; set; } = OfkEnv.PythonExe;

        [Display(Name = "Script .py path", GroupName = "09.Floating Panel", Order = 3)]
        public string ScriptPath { get; set; } = Path.Combine(OfkEnv.Pipeline, "run_morning_ES.py");

        [Display(Name = "Briefing PDF folder", GroupName = "09.Floating Panel", Order = 4)]
        public string BriefingDir { get; set; } = OfkEnv.Data;

        [Display(Name = "Intraday refresh script", GroupName = "09.Floating Panel", Order = 5,
                 Description = "Background .py script launched when 'Intraday loop' is ON")]
        public string IntradayRefreshScriptPath { get; set; } = Path.Combine(OfkEnv.Pipeline, "run_intraday_refresh.py");

        #endregion

        #region 10.Scalping Alerts

        [Display(Name = "Sound file (.wav)", GroupName = "10.Scalping Alerts", Order = 1,
                 Description = "Audio file played for alerts (alert1.wav, alert2.wav, …)")]
        public string AlertSoundFile { get; set; } = "alert1.wav";

        [Display(Name = "Scalping alerts enabled", GroupName = "10.Scalping Alerts", Order = 2)]
        public bool EnableScalpingAlerts { get; set; } = true;

        [Display(Name = "Alert cooldown (s)", GroupName = "10.Scalping Alerts", Order = 3,
                 Description = "Minimum duration between two alerts of the same type")]
        [Range(10, 3600)]
        public int AlertCooldownSeconds { get; set; } = 120;

        [Display(Name = "Proximity ticks (Pin/Charm)", GroupName = "10.Scalping Alerts", Order = 4)]
        [Range(1, 100)]
        public int AlertProximityTicks { get; set; } = 5;

        [Display(Name = "Predictive alerts (cross approach)", GroupName = "10.Scalping Alerts", Order = 5,
                 Description = "Triggers an alert BEFORE price crosses a level (gamma flip, walls, trans, vol flow)")]
        public bool EnablePredictiveAlerts { get; set; } = true;

        [Display(Name = "Predictive proximity (ticks)", GroupName = "10.Scalping Alerts", Order = 6,
                 Description = "Distance to level to trigger the approach alert")]
        [Range(1, 100)]
        public int PredictiveAlertProximityTicks { get; set; } = 10;

        [Display(Name = "1. Cross Gamma Flip", GroupName = "10.Scalping Alerts", Order = 10)]
        public bool AlertCrossGammaFlip { get; set; } = true;

        [Display(Name = "2. Cross Call/Put Wall (intraday)", GroupName = "10.Scalping Alerts", Order = 11)]
        public bool AlertCrossWalls { get; set; } = true;

        [Display(Name = "3. Cross cTrans/pTrans", GroupName = "10.Scalping Alerts", Order = 12)]
        public bool AlertCrossTrans { get; set; } = true;

        [Display(Name = "4. Pin Strike 0DTE approach", GroupName = "10.Scalping Alerts", Order = 13)]
        public bool AlertPin0DTE { get; set; } = true;

        [Display(Name = "5. Charm Magnet approach (last hour)", GroupName = "10.Scalping Alerts", Order = 14)]
        public bool AlertCharmMagnet { get; set; } = true;

        [Display(Name = "6. Extreme IVR on load (>90 or <10)", GroupName = "10.Scalping Alerts", Order = 15)]
        public bool AlertIvrExtreme { get; set; } = true;

        [Display(Name = "7. Acute term backwardation (>1vp)", GroupName = "10.Scalping Alerts", Order = 16)]
        public bool AlertTermBackwardation { get; set; } = true;

        [Display(Name = "8. Explosive skew (>5vp)", GroupName = "10.Scalping Alerts", Order = 17)]
        public bool AlertSkewExplosive { get; set; } = true;

        [Display(Name = "9. VIX enters EXTREME regime", GroupName = "10.Scalping Alerts", Order = 18)]
        public bool AlertVixRegimeChange { get; set; } = true;

        [Display(Name = "10. Volume Flow breach (GEX ext)", GroupName = "10.Scalping Alerts", Order = 19)]
        public bool AlertVolumeFlowBreach { get; set; } = true;

        [Display(Name = "11. Imminent macro blackout (<30min)", GroupName = "10.Scalping Alerts", Order = 20)]
        public bool AlertMacroBlackout { get; set; } = true;

        [Display(Name = "12. Stale data (>10min)", GroupName = "10.Scalping Alerts", Order = 21)]
        public bool AlertStaleData { get; set; } = true;

        [Display(Name = "On-chart banner", GroupName = "11.Visual Alerts", Order = 1,
                 Description = "Displays alerts directly on the chart (top-right corner)")]
        public bool EnableVisualBanners { get; set; } = true;

        [Display(Name = "Banner duration (s)", GroupName = "11.Visual Alerts", Order = 2)]
        [Range(5, 300)]
        public int BannerDurationSeconds { get; set; } = 30;

        [Display(Name = "Max banners displayed", GroupName = "11.Visual Alerts", Order = 3)]
        [Range(1, 10)]
        public int MaxVisibleBanners { get; set; } = 5;

        [Display(Name = "Intraday snapshots folder", GroupName = "12.Intraday Replay", Order = 1,
                 Description = "Path to folder of timestamped snapshots (5 min) for replay")]
        public string IntradayHistoryDir { get; set; } = Path.Combine(OfkEnv.Data, "history", "intraday");

        #endregion

        #region Private

        private volatile GexSnapshot  _levels       = GexSnapshot.Empty;
        private volatile MetaSnapshot _meta         = MetaSnapshot.Empty;
        private string   _loadedDate  = "";
        private bool     _levelsLoaded = false;
        private DateTime _lastLoadTime = DateTime.MinValue;
        private DateTime _jsonSeen, _jsonChecked; // JSON last write already loaded / last disk check

        // Intraday replay
        private volatile bool _replayMode = false;
        private DateTime      _replayTimestamp = DateTime.MinValue;

        // Intraday loop refresh (background process)
        private Process?      _loopProcess = null;
        private volatile bool _loopRunning = false;
        private DateTime      _loopStartedAt = DateTime.MinValue;

        // Alert anti-spam: key → last emission
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> _lastAlerts = new();
        private string _lastVixRegime = "";

        private struct BannerEntry
        {
            public string Message;
            public DrawingColor Color;
            public DateTime Time;
        }
        private readonly System.Collections.Generic.List<BannerEntry> _banners = new();
        private readonly object _bannerSync = new object();

        // Alert stats (day + last 7 days counters)
        private readonly System.Collections.Generic.Dictionary<string, (int today, int week)> _alertStats = new();
        private readonly object _alertStatsSync = new object();
        private const string AlertLogFileName = "alerts_log_ES.txt";

        // Predictive alerts hysteresis state (true = within proximity zone)
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _approachState = new();

        // Font managed dynamically via GetLabelFont() which respects LabelFontSize.

        private volatile bool _isRunning = false;

        #endregion

        public OFK_ES_GEX_Levels() : base(true)
        {
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Historical | DrawingLayouts.LatestBar | DrawingLayouts.Final);
            DenyToChangePanel = true;
            DataSeries[0].IsHidden = true;
            ((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar == 0)
            {
                if (!_levelsLoaded) LoadLevels();
            }
            if (!_levelsLoaded)
            {
                // Not loaded yet (file missing / invalid): retry when it changes on disk.
                if (bar >= CurrentBar - 1 && OfkUtils.FileChanged(JsonPath, _jsonSeen, ref _jsonChecked))
                    LoadLevels();
                if (!_levelsLoaded) return;
            }

            bool isLastBar = bar >= CurrentBar - 1;
            bool newDay    = _loadedDate != OfkEnv.NowEt.ToString("yyyy-MM-dd");
            bool elapsed   = RefreshMinutes > 0 &&
                             (DateTime.Now - _lastLoadTime).TotalMinutes >= RefreshMinutes;
            bool changed   = isLastBar && OfkUtils.FileChanged(JsonPath, _jsonSeen, ref _jsonChecked);
            if (isLastBar && (newDay || elapsed || changed)) { LoadLevels(); UpdatePanelText(); }

            // Scalping alerts (live bar only)
            if (isLastBar && EnableScalpingAlerts) CheckAlerts(bar);
        }

        // ── Scalping alerts (Block 4) ────────────────────────────────────────
        private void CheckAlerts(int bar)
        {
            if (!_levelsLoaded || bar < 1 || bar >= CurrentBar) return;
            var lv = _levels;
            if (!lv.Loaded) return;

            var prev = GetCandle(bar - 1);
            var curr = GetCandle(bar);
            if (prev == null || curr == null) return;

            decimal prevClose = prev.Close;
            decimal currClose = curr.Close;
            decimal tick = InstrumentInfo?.TickSize ?? 0.25m;

            // 1. Cross Gamma Flip
            if (AlertCrossGammaFlip && CrossDetected(prevClose, currClose, (decimal)lv.GammaFlip))
                FireAlert("gamma_flip",
                    $"ES {currClose:0} crosses Gamma Flip {lv.GammaFlip:0} — gamma regime change",
                    curr);

            // 2. Cross Call/Put Wall intraday
            if (AlertCrossWalls)
            {
                if (CrossDetected(prevClose, currClose, (decimal)lv.CallWallIntraday))
                    FireAlert("call_wall_id", $"ES crosses Call Wall intraday {lv.CallWallIntraday:0}", curr);
                if (CrossDetected(prevClose, currClose, (decimal)lv.PutWallIntraday))
                    FireAlert("put_wall_id", $"ES crosses Put Wall intraday {lv.PutWallIntraday:0}", curr);
            }

            // 3. cTrans / pTrans (gamma zone)
            if (AlertCrossTrans)
            {
                if (CrossDetected(prevClose, currClose, (decimal)lv.CTransIntraday))
                    FireAlert("c_trans", $"ES crosses cTrans {lv.CTransIntraday:0} — gamma zone change", curr);
                if (CrossDetected(prevClose, currClose, (decimal)lv.PTransIntraday))
                    FireAlert("p_trans", $"ES crosses pTrans {lv.PTransIntraday:0} — gamma zone change", curr);
            }

            // Predictive alerts (approach before cross) — toggles tied to the cross alerts above
            if (EnablePredictiveAlerts)
            {
                if (AlertCrossGammaFlip)
                    CheckApproach("gamma_flip", currClose, (decimal)lv.GammaFlip, tick, curr, "ES", "Gamma Flip");
                if (AlertCrossWalls)
                {
                    CheckApproach("call_wall_id", currClose, (decimal)lv.CallWallIntraday, tick, curr, "ES", "Call Wall ID");
                    CheckApproach("put_wall_id",  currClose, (decimal)lv.PutWallIntraday,  tick, curr, "ES", "Put Wall ID");
                }
                if (AlertCrossTrans)
                {
                    CheckApproach("c_trans", currClose, (decimal)lv.CTransIntraday, tick, curr, "ES", "cTrans");
                    CheckApproach("p_trans", currClose, (decimal)lv.PTransIntraday, tick, curr, "ES", "pTrans");
                }
                if (AlertVolumeFlowBreach)
                    CheckApproach("vol_flow", currClose, (decimal)lv.GexExt1, tick, curr, "ES", "GEX ext-1");
            }

            // 4. Pin Strike 0DTE proximity
            if (AlertPin0DTE && lv.PinStrike0DTE > 0)
            {
                decimal dist = Math.Abs(currClose - (decimal)lv.PinStrike0DTE);
                if (dist <= AlertProximityTicks * tick)
                    FireAlert("pin_0dte",
                        $"ES near Pin Strike 0DTE {lv.PinStrike0DTE:0} (±{AlertProximityTicks}t)",
                        curr);
            }

            // 5. Charm Magnet — last RTH hour only
            if (AlertCharmMagnet && lv.CharmMagnet0DTE > 0 && IsLastHourRTH())
            {
                decimal dist = Math.Abs(currClose - (decimal)lv.CharmMagnet0DTE);
                if (dist <= AlertProximityTicks * tick * 3)
                    FireAlert("charm_magnet",
                        $"ES approaching Charm Magnet 0DTE {lv.CharmMagnet0DTE:0} (last hour)",
                        curr);
            }

            // 6. Extreme IVR (only if history is sufficient)
            if (AlertIvrExtreme && lv.IvRankIntraday >= 0
                && (lv.IvRankIntradayStatus == "ok" || lv.IvRankIntradayStatus == "partial"))
            {
                if (lv.IvRankIntraday > 90)
                    FireAlert("ivr_high", $"IVR very high {lv.IvRankIntraday:0}% — high vol, widen stops", curr);
                else if (lv.IvRankIntraday < 10)
                    FireAlert("ivr_low", $"IVR very low {lv.IvRankIntraday:0}% — compressed vol, tight range", curr);
            }

            // 7. Acute term backwardation (slope > 1 vp)
            if (AlertTermBackwardation && lv.TermIntradaySlope > 0.01)
                FireAlert("term_back",
                    $"Acute term backwardation (slope +{lv.TermIntradaySlope * 100:0.0}vp) — STRESS, breakouts",
                    curr);

            // 8. Explosive skew (>5 vp)
            if (AlertSkewExplosive && lv.Skew25dIntraday > 0.05)
                FireAlert("skew_high",
                    $"Explosive Skew 25Δ {lv.Skew25dIntraday * 100:0.0}vp — aggressive put protection",
                    curr);

            // 9. VIX regime change → extreme
            if (AlertVixRegimeChange && _meta.Loaded && !string.IsNullOrEmpty(_meta.VixRegime))
            {
                if (_meta.VixRegime != _lastVixRegime && _meta.VixRegime == "extreme")
                    FireAlert("vix_extreme",
                        $"VIX EXTREME regime ({_meta.Vix:0.0}, DoD {_meta.VixDodChange:+0.0;-0.0;0}) — avoid scalping",
                        curr);
                _lastVixRegime = _meta.VixRegime;
            }

            // 10. Volume Flow breach (cross GEX ext-1)
            if (AlertVolumeFlowBreach && lv.GexExt1 > 0 &&
                CrossDetected(prevClose, currClose, (decimal)lv.GexExt1))
                FireAlert("vol_flow", $"ES crosses GEX ext-1 {lv.GexExt1:0} — directional flow", curr);

            // 11. Imminent macro blackout (<30 min)
            if (AlertMacroBlackout && _meta.Loaded &&
                _meta.MacroMinutesToNext > 0 && _meta.MacroMinutesToNext <= 30)
                FireAlert("macro_imminent",
                    $"Macro event {_meta.MacroNextEventTitle} in {_meta.MacroMinutesToNext}min — STOP scalping",
                    curr);

            // 12. Stale / partial / version mismatch (Block 7 health checks)
            if (AlertStaleData)
            {
                // Stale = the JSON on disk has not been regenerated for >10min,
                // but only if Intraday loop is ON (otherwise the user manages
                // their refresh manually → no need to spam).
                if (_loopRunning)
                {
                    try
                    {
                        if (File.Exists(JsonPath))
                        {
                            // Age since the later of the last write and the loop start:
                            // right after turning the loop ON the old file is not "frozen"
                            // (the first cycle needs a few seconds to rewrite it).
                            var lastWrite = File.GetLastWriteTime(JsonPath);
                            var since = lastWrite > _loopStartedAt ? lastWrite : _loopStartedAt;
                            var ageMin = (DateTime.Now - since).TotalMinutes;
                            if (ageMin > 10)
                                FireAlert("stale_data",
                                    $"Intraday loop active but JSON frozen ({(int)ageMin}min) — check the Python process",
                                    curr);
                        }
                    }
                    catch { }
                }
                if (_meta.Loaded && _meta.DataQuality == "partial")
                    FireAlert("data_partial",
                        $"Pipeline data PARTIAL (CME or CBOE missing) — degraded quality",
                        curr);
                if (_meta.Loaded && _meta.DataQuality == "error")
                    FireAlert("data_error",
                        $"Pipeline data ERROR — no valid levels",
                        curr);
                if (_meta.Loaded && !string.IsNullOrEmpty(_meta.JsonSchemaVersion) &&
                    _meta.JsonSchemaVersion != EXPECTED_JSON_SCHEMA_VERSION)
                    FireAlert("schema_mismatch",
                        $"JSON schema {_meta.JsonSchemaVersion} ≠ expected {EXPECTED_JSON_SCHEMA_VERSION} — pipeline schema desync vs indicator",
                        curr);
            }
        }

        private static bool CrossDetected(decimal prev, decimal curr, decimal level)
        {
            if (level <= 0) return false;
            return (prev < level && curr >= level) || (prev > level && curr <= level);
        }

        // Predictive alert with hysteresis: triggers on zone entry, resets on widened exit
        private void CheckApproach(string baseKey, decimal price, decimal level, decimal tick, IndicatorCandle candle, string instrumentLabel, string levelLabel)
        {
            if (level <= 0 || !EnablePredictiveAlerts) return;
            decimal dist = Math.Abs(price - level);
            decimal proximity = PredictiveAlertProximityTicks * tick;
            decimal exitThreshold = proximity * 2m;

            string key = baseKey + "_approach";
            bool wasInZone = _approachState.TryGetValue(key, out bool prev) && prev;

            if (dist <= proximity && !wasInZone)
            {
                int distTicks = (int)(dist / tick);
                FireAlert(key, $"{instrumentLabel} approaching {levelLabel} {level:F0} (at {distTicks} ticks)", candle);
                _approachState[key] = true;
            }
            else if (dist > exitThreshold && wasInZone)
            {
                _approachState[key] = false;
            }
        }

        private static bool IsLastHourRTH()
        {
            // Last RTH hour = 15:00-16:00 New York time. Fixed UTC hours were
            // off by 1 h half of the year (US DST); OfkEnv.NowEt is DST-aware.
            return OfkEnv.NowEt.Hour == 15;
        }

        private void FireAlert(string key, string message, IndicatorCandle candle)
        {
            if (!_lastAlerts.TryGetValue(key, out var last)) last = DateTime.MinValue;
            if ((DateTime.Now - last).TotalSeconds < AlertCooldownSeconds) return;
            try
            {
                string sound = string.IsNullOrWhiteSpace(AlertSoundFile) ? "alert1.wav" : AlertSoundFile;
                var bg = DrawingColor.FromArgb(255, 50, 50, 50);
                var fg = DrawingColor.FromArgb(255, 255, 200, 80);
                AddAlert(sound, InstrumentInfo?.Instrument ?? "", message, bg, fg);
                _lastAlerts[key] = DateTime.Now;

                lock (_alertStatsSync)
                {
                    if (_alertStats.TryGetValue(key, out var cur))
                        _alertStats[key] = (cur.today + 1, cur.week + 1);
                    else
                        _alertStats[key] = (1, 1);
                }

                if (EnableVisualBanners)
                {
                    lock (_bannerSync)
                    {
                        _banners.Add(new BannerEntry { Message = message, Color = GetBannerColor(key), Time = DateTime.Now });
                        while (_banners.Count > MaxVisibleBanners * 2) _banners.RemoveAt(0);
                    }
                }

                // File log append (Block 8 stats) — history readable outside session
                try
                {
                    string dir = Path.GetDirectoryName(JsonPath) ?? "";
                    string logPath = Path.Combine(dir, "alerts_log_ES.txt");
                    File.AppendAllText(logPath,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{key}] {message}\n");
                }
                catch { /* log file optional */ }
            }
            catch { /* AddAlert signature can vary across ATAS versions */ }
        }

        private static DrawingColor GetBannerColor(string key)
        {
            if (key == "vix_extreme" || key == "macro_imminent")
                return DrawingColor.FromArgb(255, 255, 60, 60);
            if (key == "ivr_high" || key == "ivr_low" || key == "term_back" || key == "skew_high")
                return DrawingColor.FromArgb(255, 255, 140, 0);
            if (key == "pin_0dte" || key == "charm_magnet")
                return DrawingColor.FromArgb(255, 0, 200, 255);
            if (key.Contains("data") || key.Contains("schema") || key.Contains("stale"))
                return DrawingColor.FromArgb(255, 140, 140, 150);
            return DrawingColor.FromArgb(255, 255, 200, 0);
        }

        // ── Dynamic position sizing (VIX × macro × data_quality) ────────────
        private static (int pct, string reason, string tag) ComputePositionSizing(MetaSnapshot meta)
        {
            if (!meta.Loaded)
                return (100, "No _meta context — default sizing", "OK");

            double pct = 1.0;
            var reasons = new System.Collections.Generic.List<string>();

            // VIX regime
            if (meta.VixRegime == "extreme")  { pct *= 0.25; reasons.Add($"VIX EXTREME ({meta.Vix:F1})"); }
            else if (meta.VixRegime == "elevated") { pct *= 0.60; reasons.Add($"VIX elevated ({meta.Vix:F1})"); }

            // Macro events
            if (meta.MacroInBlackout)
            {
                pct = 0.0;
                reasons.Add("MACRO BLACKOUT IN PROGRESS");
            }
            else if (meta.MacroMinutesToNext > 0 && meta.MacroMinutesToNext <= 30)
            {
                pct = 0.0;
                string ev = string.IsNullOrEmpty(meta.MacroNextEventTitle) ? "macro event" : meta.MacroNextEventTitle;
                reasons.Add($"{ev} in {meta.MacroMinutesToNext}min");
            }
            else if (meta.MacroMinutesToNext > 0 && meta.MacroMinutesToNext <= 60)
            {
                pct *= 0.50;
                string ev = string.IsNullOrEmpty(meta.MacroNextEventTitle) ? "macro event" : meta.MacroNextEventTitle;
                reasons.Add($"{ev} in {meta.MacroMinutesToNext}min");
            }

            // Data quality
            if (meta.DataQuality == "error")   { pct = 0.0; reasons.Add("Pipeline data ERROR"); }
            else if (meta.DataQuality == "partial") { pct *= 0.70; reasons.Add("Partial data"); }

            int finalPct = (int)Math.Round(Math.Max(0.0, Math.Min(1.0, pct)) * 100);
            string reason = reasons.Count > 0 ? string.Join(", ", reasons) : "Normal conditions";
            string tag = finalPct >= 80 ? "OK"
                       : finalPct >= 50 ? "CAUTION"
                       : finalPct > 0   ? "HIGH CAUTION"
                       :                  "FLAT";
            return (finalPct, reason, tag);
        }

        private static string SizingBar(int pct)
        {
            int filled = (int)Math.Round(pct / 20.0); // 0 to 5 segments
            if (filled < 0) filled = 0;
            if (filled > 5) filled = 5;
            return new string('●', filled) + new string('○', 5 - filled);
        }

        // ── Alert stats (day + 7-day counters) ───────────────────────────────
        private void LoadAlertStats()
        {
            try
            {
                string dir = Path.GetDirectoryName(JsonPath) ?? "";
                string logPath = Path.Combine(dir, AlertLogFileName);
                if (!File.Exists(logPath)) return;

                // "Today" = current US session (New York date); log lines are local time.
                DateTime today = OfkEnv.NowEt.Date;
                DateTime weekCutoff = today.AddDays(-7);
                var fresh = new System.Collections.Generic.Dictionary<string, (int today, int week)>();

                foreach (var raw in File.ReadLines(logPath))
                {
                    string line = raw?.Trim() ?? "";
                    if (line.Length < 22) continue;
                    if (!DateTime.TryParseExact(line.Substring(0, 19), "yyyy-MM-dd HH:mm:ss",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out var dt))
                        continue;
                    int b1 = line.IndexOf('[', 19);
                    int b2 = b1 >= 0 ? line.IndexOf(']', b1 + 1) : -1;
                    if (b1 < 0 || b2 < 0 || b2 <= b1 + 1) continue;
                    string key = line.Substring(b1 + 1, b2 - b1 - 1);
                    if (string.IsNullOrEmpty(key)) continue;

                    DateTime dtEt = OfkEnv.ToEt(DateTime.SpecifyKind(dt, DateTimeKind.Local));
                    int isToday = dtEt >= today ? 1 : 0;
                    int isWeek  = dtEt >= weekCutoff ? 1 : 0;
                    if (!fresh.TryGetValue(key, out var cur)) cur = (0, 0);
                    fresh[key] = (cur.today + isToday, cur.week + isWeek);
                }

                lock (_alertStatsSync)
                {
                    _alertStats.Clear();
                    foreach (var kv in fresh) _alertStats[kv.Key] = kv.Value;
                }
            }
            catch { /* best-effort */ }
        }

        private string FormatAlertStats()
        {
            System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, (int today, int week)>> ordered;
            int totalToday = 0, totalWeek = 0;
            lock (_alertStatsSync)
            {
                if (_alertStats.Count == 0) return "  No alerts recorded\n";
                foreach (var kv in _alertStats) { totalToday += kv.Value.today; totalWeek += kv.Value.week; }
                ordered = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, (int, int)>>();
                foreach (var kv in _alertStats) ordered.Add(kv);
            }
            ordered.Sort((a, b) =>
            {
                int c = b.Value.today.CompareTo(a.Value.today);
                return c != 0 ? c : b.Value.week.CompareTo(a.Value.week);
            });

            var sb = new System.Text.StringBuilder();
            int shown = 0;
            foreach (var kv in ordered)
            {
                if (shown >= 6) break;
                sb.AppendLine($"  {kv.Key,-16} {kv.Value.today,2} j /{kv.Value.week,3} 7j");
                shown++;
            }
            sb.AppendLine($"  {"TOTAL",-16} {totalToday,2} j /{totalWeek,3} 7j");
            return sb.ToString();
        }

        // ── Panel ─────────────────────────────────────────────────────────────

        private void UpdatePanelText()
        {
            {
                var lv = _levels;

                string gReg  = lv.GexRegime > 0 ? "POSITIVE ● pinning" : lv.GexRegime < 0 ? "NEGATIVE ● explosive" : "NEUTRAL";
                string vReg  = lv.VexRegime > 0 ? "IV↓ = RALLY ▲"     : lv.VexRegime < 0 ? "IV↑ = SELLOFF ▼"   : "neutral";
                string dSide = lv.DexTotal  > 0 ? "longs" : "shorts";
                string pcrStr= lv.Pcr > 0 ? $"{lv.Pcr:F3}  ({(lv.Pcr > 1 ? "put-heavy" : "call-heavy")})" : "—";
                // Intraday (scalping primary)
                string ivxIdStr  = lv.AtmIvIntraday > 0
                    ? $"{lv.AtmIvIntraday*100:F1}%  ({lv.AtmIvIntradayDte}d)"
                    : "—";
                string skewIdStr = lv.Skew25dIntraday != 0
                    ? $"{lv.Skew25dIntraday*100:+0.00;-0.00} vp  ({lv.Skew25dIntradayDte}d, {(lv.Skew25dIntraday > 0 ? "bearish" : "bullish")})"
                    : "—";
                string termIdStr = (!string.IsNullOrEmpty(lv.TermIntradayRegime) && lv.TermIntradayRegime != "unknown")
                    ? $"{lv.TermIntradayIvFront*100:F1}% ({lv.TermIntradayFrontDte}d) → {lv.TermIntradayIvBack*100:F1}% ({lv.TermIntradayBackDte}d) [{lv.TermIntradayRegime}, {lv.TermIntradaySlope*100:+0.00;-0.00} vp]"
                    : "—";
                // Structural (CME 49d secondary)
                string ivxStrStr  = lv.AtmIvStructural > 0 ? $"{lv.AtmIvStructural*100:F1}%" : "—";
                string skewStrStr = lv.Skew25dStructural != 0
                    ? $"{lv.Skew25dStructural*100:+0.00;-0.00} vp"
                    : "—";
                string termStrStr = (lv.IvStructuralBack > 0 && !string.IsNullOrEmpty(lv.TermStructuralRegime) && lv.TermStructuralRegime != "unknown")
                    ? $"{lv.AtmIvStructural*100:F1}% → {lv.IvStructuralBack*100:F1}% ({lv.IvStructuralBackDte}d, {lv.TermStructuralRegime}, {lv.TermStructuralSlope*100:+0.00;-0.00} vp)"
                    : "—";
                string emStr = lv.ExpectedMovePts > 0 ? $"±{lv.ExpectedMovePts:F0} pts  [{lv.ExpectedMoveLow:F0} — {lv.ExpectedMoveHigh:F0}]" : "—";
                string oi1   = lv.TopOI1 > 0 ? $"  Top OI #1   {lv.TopOI1:F0}  (OI {lv.TopOI1Vol:N0})\n" : "";
                string oi2   = lv.TopOI2 > 0 ? $"  Top OI #2   {lv.TopOI2:F0}  (OI {lv.TopOI2Vol:N0})\n" : "";
                string oi3   = lv.TopOI3 > 0 ? $"  Top OI #3   {lv.TopOI3:F0}  (OI {lv.TopOI3Vol:N0})\n" : "";

                int idDte = lv.WallsIntradayMaxDte > 0 ? lv.WallsIntradayMaxDte : 7;
                string cwIdStr = lv.CallWallIntraday > 0 ? $"{lv.CallWallIntraday:F0}" : "—";
                string pwIdStr = lv.PutWallIntraday  > 0 ? $"{lv.PutWallIntraday:F0}" : "—";
                string ctStr   = lv.CTransIntraday   > 0 ? $"{lv.CTransIntraday:F0}"   : "—";
                string ptStr   = lv.PTransIntraday   > 0 ? $"{lv.PTransIntraday:F0}"   : "—";
                string transLine = (lv.CTransIntraday > 0 || lv.PTransIntraday > 0)
                    ? $"  cTrans      {ctStr}     pTrans    {ptStr}\n" : "";
                string dpStr   = lv.DexPlusIntraday  > 0 ? $"{lv.DexPlusIntraday:F0}"  : "—";
                string dmStr   = lv.DexMinusIntraday > 0 ? $"{lv.DexMinusIntraday:F0}" : "—";
                string dexLine = (lv.DexPlusIntraday > 0 || lv.DexMinusIntraday > 0)
                    ? $"  D+ DEX      {dpStr}     D- DEX    {dmStr}\n" : "";
                string oid1 = lv.TopOIIntraday1 > 0 ? $"  OI ID #1    {lv.TopOIIntraday1:F0}  (OI {lv.TopOIIntraday1Vol:N0})\n" : "";
                string oid2 = lv.TopOIIntraday2 > 0 ? $"  OI ID #2    {lv.TopOIIntraday2:F0}  (OI {lv.TopOIIntraday2Vol:N0})\n" : "";
                string oid3 = lv.TopOIIntraday3 > 0 ? $"  OI ID #3    {lv.TopOIIntraday3:F0}  (OI {lv.TopOIIntraday3Vol:N0})\n" : "";
                // IV Rank
                string ivrStr = "";
                if (lv.IvRankIntraday > 0 && (lv.IvRankIntradayStatus == "ok" || lv.IvRankIntradayStatus == "partial"))
                    ivrStr = $"  IVR         {lv.IvRankIntraday:F0}%  ({lv.IvRankIntradayStatus})\n";
                else if (!string.IsNullOrEmpty(lv.IvRankIntradayStatus))
                    ivrStr = $"  IVR         {lv.IvRankIntradayStatus} (insufficient history)\n";
                // 0DTE
                string zdLabel = lv.ZeroDTEDte == 0 ? "0DTE" : $"{lv.ZeroDTEDte}DTE";
                string mp0   = lv.MaxPain0DTE     > 0 ? $"  Max Pain    {lv.MaxPain0DTE:F0}  ({zdLabel})\n" : "";
                string pin0  = lv.PinStrike0DTE   > 0 ? $"  Pin Strike  {lv.PinStrike0DTE:F0}  ({zdLabel})\n" : "";
                string ch0   = lv.CharmMagnet0DTE > 0 ? $"  Charm Mag.  {lv.CharmMagnet0DTE:F0}  ({zdLabel})\n" : "";
                string zdSection = (mp0 + pin0 + ch0).Length > 0
                    ? $"━━ 0DTE (session end) ━━\n{mp0}{pin0}{ch0}  Total OI {zdLabel}: {lv.ZeroDTEOITotal:N0}\n\n"
                    : "";

                var (sizingPct, sizingReason, sizingTag) = ComputePositionSizing(_meta);
                string sizingBar = SizingBar(sizingPct);
                string sizingLine = $"━━ POSITION SIZING [{sizingTag}] ━━\n  {sizingPct,3}%   {sizingBar}   {sizingReason}\n\n";

                string alertStatsLine = $"━━ ALERT STATS (day / 7d) ━━\n{FormatAlertStats()}\n";

                _panelText =
                    $"═══ OPTIONS GREEKS ES  ({lv.TradeDate}) ═══\n\n" +
                    sizingLine +
                    alertStatsLine +
                    $"  GEX  {lv.GexTotal / 1e9:+0.000;-0.000}B   {gReg}\n" +
                    $"  VEX  {lv.VexTotal / 1e8:+0.00;-0.00}       {vReg}\n" +
                    $"  CEX  {lv.CexTotal / 1e6:+0.00;-0.00}M\n" +
                    $"  DEX  {lv.DexTotal / 1e10:+0.000;-0.000}   dealers {dSide}\n\n" +
                    $"  Gamma Flip  {lv.GammaFlip:F0}     Trigger  {lv.VolTrigger:F0}\n" +
                    $"  Risk Pivot  {lv.RiskPivot:F0}    V-Flip   {lv.VannaFlip:F0}\n" +
                    $"  Charm       {lv.CharmMagnet:F0}     Spot ref. {lv.SpotLoaded:F0}\n\n" +
                    $"  Max Pain    {lv.MaxPain:F0}\n" +
                    $"  Exp. Move   {emStr}\n" +
                    $"  PCR         {pcrStr}\n\n" +
                    $"━━ INTRADAY (scalping, 0-{idDte}d) ━━\n" +
                    $"  IVx         {ivxIdStr}\n" +
                    ivrStr +
                    $"  Skew 25Δ    {skewIdStr}\n" +
                    $"  Term IV     {termIdStr}\n" +
                    $"  Call Wall   {cwIdStr}     Put Wall  {pwIdStr}\n" +
                    transLine +
                    dexLine +
                    oid1 + oid2 + oid3 + "\n" +
                    zdSection +
                    $"━━ Structural (CME 49d, all-exp) ━━\n" +
                    $"  IVx         {ivxStrStr}\n" +
                    $"  Skew 25Δ    {skewStrStr}\n" +
                    $"  Term IV     {termStrStr}\n" +
                    $"  Call Wall   {lv.CallWall:F0}     Put Wall  {lv.PutWall:F0}\n" +
                    oi1 + oi2 + oi3;
            }
        }

        // ── ATAS X: on-chart panel (replaces the WPF floating window) ─────────
        // ATAS X rejects WPF (Window/Button/Dispatcher): the panel is drawn in
        // OnRender and its buttons are hit-tested in ProcessMouseClick.
        private volatile string _panelText = "";
        private volatile string _statusMsg = "";
        private System.Collections.Generic.List<GexLoader.SnapshotInfo> _replaySnaps = new();
        private int _replayIdx = -1;
        private volatile HitArea[] _hits = Array.Empty<HitArea>();
        private readonly record struct HitArea(Rectangle Rect, Action Action);

        private void SetStatus(string msg)
        {
            _statusMsg = msg;
            try { RedrawChart(); } catch { }
        }

        private static string Short(string s) => s.Substring(0, Math.Min(60, s.Length));

        // ATAS draws its status line (OHLC + latency badge) and the indicator list
        // at the top-left of the price pane. Like the native TradingPanel, shift
        // them right of the panel while it is shown; restore 3 px (ATAS default).
        private int _overlayOffset = -1; // -1 = untouched

        private void SetChartOverlayOffset(int x)
        {
            if (x == _overlayOffset || ChartInfo == null) return;
            try
            {
                ChartInfo.IndicatorsListHorizontalOffset = x;
                ChartInfo.StatusLineHorizontalOffset     = x;
                _overlayOffset = x;
            }
            catch { }
        }

        private void RestoreChartOverlay()
        {
            if (_overlayOffset < 0) return;
            SetChartOverlayOffset(3);
            _overlayOffset = -1;
        }

        private void DrawPanel(RenderContext ctx)
        {
            if (!ShowPanel) { _hits = Array.Empty<HitArea>(); RestoreChartOverlay(); return; }
            var lv     = _levels;
            var font   = new RenderFont("Consolas", PanelFontSize);
            var bFont  = new RenderFont("Segoe UI", PanelFontSize);
            var hFont  = new RenderFont("Segoe UI", PanelFontSize + 1, FontStyle.Bold);
            int a      = Math.Max(20, Math.Min(100, PanelOpacity)) * 255 / 100;
            var cBg    = DrawingColor.FromArgb(a, 13, 17, 23);
            var cBord  = DrawingColor.FromArgb(255, 33, 41, 61);
            var cText  = DrawingColor.FromArgb(255, 201, 209, 217);
            var cDim   = DrawingColor.FromArgb(255, 139, 148, 158);
            var cAcc   = DrawingColor.FromArgb(255, 79, 139, 209);

            string header = (PanelCollapsed ? "► " : "▼ ") + "OFK GEX ES" + (_replayMode ? $"   REPLAY {_replayTimestamp:HH:mm}" : "");
            string status = !string.IsNullOrEmpty(_statusMsg) ? _statusMsg
                          : lv.Loaded ? $"OK: JSON loaded — {lv.TradeDate}  (spot {lv.SpotLoaded:F0})"
                          : "! JSON not loaded — check JSON Path";
            string[] lines = PanelCollapsed ? Array.Empty<string>() : (_panelText ?? "").TrimEnd('\n').Split('\n');

            var rows = new[]
            {
                new (string label, DrawingColor color, Action action)[]
                {
                    (_isRunning ? "Running…" : "► GEX LEVELS ES", cAcc, () => RunScript()),
                    ("Briefing PDF", DrawingColor.FromArgb(255, 63, 185, 80), () => OpenBriefing()),
                },
                new (string label, DrawingColor color, Action action)[]
                {
                    ("◄ Replay", DrawingColor.FromArgb(255, 189, 147, 249), () => ReplayStep(-1)),
                    (_replayMode ? "Replay ►" : "● Live", _replayMode ? DrawingColor.FromArgb(255, 189, 147, 249) : cDim, () => ReplayStep(+1)),
                    (_loopRunning ? "■ Loop: ON" : "► Loop: OFF",
                        _loopRunning ? DrawingColor.FromArgb(255, 63, 185, 80) : cDim, () => ToggleLoop()),
                },
            };

            int pad   = 8;
            int lineH = (int)ctx.MeasureString("Ag", font).Height + 1;
            int headH = (int)ctx.MeasureString(header, hFont).Height + 8;
            int btnH  = (int)ctx.MeasureString("Ag", bFont).Height + 10;

            var region = ChartInfo?.PriceChartContainer?.Region ?? Rectangle.Empty;
            if (region.Height <= 0) region = new Rectangle(0, 0, ChartArea.Width, ChartArea.Height);
            int x0 = region.X + PanelX, y0 = region.Y + PanelTop;

            // Header, buttons and status always fit; the text below is cut at the
            // bottom of the price pane (sub-panes such as the Context Score start
            // there) with a "+N lines" note instead of hiding the buttons.
            int fixedH = headH + rows.Length * (btnH + 4) + lineH + pad;
            int room   = region.Bottom - y0 - fixedH - pad;
            int fit    = lines.Length == 0 ? 0 : Math.Max(0, (room - pad) / lineH);
            bool cut   = fit < lines.Length;
            int shown  = cut ? Math.Max(0, fit - 1) : lines.Length;
            string more = cut ? $"… +{lines.Length - shown} lines (collapse or lower the font size)" : null;

            int w = Math.Max(300, (int)ctx.MeasureString(header, hFont).Width + 2 * pad);
            for (int i = 0; i < shown; i++) w = Math.Max(w, (int)ctx.MeasureString(lines[i], font).Width + 2 * pad);
            w = Math.Max(w, (int)ctx.MeasureString(status, bFont).Width + 2 * pad);

            int textH = shown * lineH + (more != null ? lineH : 0);
            int h = fixedH + (textH > 0 ? pad + textH : 0) + pad;
            ctx.FillRectangle(cBg, new Rectangle(x0, y0, w, h));
            ctx.DrawRectangle(new RenderPen(cBord), new Rectangle(x0, y0, w, h));
            ctx.FillRectangle(cAcc, new Rectangle(x0, y0, w, 2));
            SetChartOverlayOffset(x0 - region.X + w + 6);

            var hits = new System.Collections.Generic.List<HitArea>();
            hits.Add(new HitArea(new Rectangle(x0, y0, w, headH), () => PanelCollapsed = !PanelCollapsed));
            ctx.DrawString(header, hFont, cText, x0 + pad, y0 + 4);

            int y = y0 + headH;
            foreach (var row in rows)
            {
                int bw = (w - 2 * pad - (row.Length - 1) * 4) / row.Length;
                for (int i = 0; i < row.Length; i++)
                {
                    var (label, color, action) = row[i];
                    var r = new Rectangle(x0 + pad + i * (bw + 4), y, bw, btnH);
                    ctx.FillRectangle(DrawingColor.FromArgb(200, color.R / 5, color.G / 5, color.B / 5), r);
                    ctx.DrawRectangle(new RenderPen(color), r);
                    var ts = ctx.MeasureString(label, bFont);
                    ctx.DrawString(label, bFont, color, r.X + (bw - (int)ts.Width) / 2, r.Y + (btnH - (int)ts.Height) / 2);
                    hits.Add(new HitArea(r, action));
                }
                y += btnH + 4;
            }
            ctx.DrawString(status, bFont, cDim, x0 + pad, y);
            y += lineH + pad;

            for (int i = 0; i < shown; i++)
            {
                var l = lines[i];
                bool section = l.StartsWith("━━") || l.StartsWith("═══");
                ctx.DrawString(l, font, section ? cAcc : cText, x0 + pad, y);
                y += lineH;
            }
            if (more != null) ctx.DrawString(more, bFont, cDim, x0 + pad, y);
            _hits = hits.ToArray();
        }

        public override bool ProcessMouseClick(RenderControlMouseEventArgs e)
        {
            if (e.Button != RenderControlMouseButtons.Left) return base.ProcessMouseClick(e);
            foreach (var hit in _hits)
            {
                if (!hit.Rect.Contains(e.X, e.Y)) continue;
                try { hit.Action(); } catch (Exception ex) { _statusMsg = "ERROR: " + Short(ex.Message); }
                try { RedrawChart(); } catch { }
                return true;
            }
            return base.ProcessMouseClick(e);
        }

        // ── RunScript ─────────────────────────────────────────────────────────

        private void RunScript()
        {
            if (_isRunning) return;
            if (!File.Exists(ScriptPath)) { SetStatus("ERROR: Script not found: " + ScriptPath); return; }

            // Immediate reload of the current JSON (it may have been updated by
            // an external run), so the user sees fresh data on click.
            LoadLevels();
            _isRunning = true;
            SetStatus("… run_morning_ES.py in progress (CME + CBOE + Claude + PDF)…");
            Task.Run(() =>
            {
                try
                {
                    // No console window: a visible console steals the keyboard focus
                    // from ATAS (and the CME collector hands the focus back to it).
                    // The full output goes to a log file instead, for diagnosis.
                    string logPath = Path.Combine(Path.GetDirectoryName(JsonPath) ?? OfkEnv.Data, "logs", "run_morning_ES_last.log");
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                    var psi = new ProcessStartInfo
                    {
                        FileName               = OfkUtils.ResolveExe(PythonExePath),
                        Arguments              = "\"" + ScriptPath + "\" --ignore-holiday",
                        UseShellExecute        = false,
                        CreateNoWindow         = true,
                        WindowStyle            = ProcessWindowStyle.Hidden,
                        WorkingDirectory       = System.IO.Path.GetDirectoryName(ScriptPath) ?? OfkEnv.Pipeline,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                    };
                    // Piped stdout defaults to cp1252 on Windows: box-drawing chars would crash Python.
                    psi.Environment["PYTHONIOENCODING"] = "utf-8";
                    psi.Environment["PYTHONUTF8"]       = "1";
                    using var log  = new StreamWriter(logPath, false, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
                    var logSync = new object();
                    using var proc = Process.Start(psi);
                    if (proc == null) throw new InvalidOperationException("python did not start");
                    // Consume both streams async: a full pipe buffer would freeze Python.
                    proc.OutputDataReceived += (s, e) => { if (e.Data != null) lock (logSync) log.WriteLine(e.Data); };
                    proc.ErrorDataReceived  += (s, e) => { if (e.Data != null) lock (logSync) log.WriteLine(e.Data); };
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    bool exited = proc.WaitForExit(300_000);
                    if (!exited) { try { proc.Kill(entireProcessTree: true); } catch { } }
                    else proc.WaitForExit(); // drain the async readers
                    int exitCode = exited ? proc.ExitCode : -99;
                    if (exitCode == 0) LoadLevels();
                    _isRunning = false;
                    SetStatus(exitCode == 0 ? $"OK: Data updated — {_levels.TradeDate}"
                              : exitCode == -99 ? "! Timeout (5 min) — see data/logs/run_morning_ES_last.log"
                              : $"! Exit {exitCode} — see data/logs/run_morning_ES_last.log");
                }
                catch (Exception ex) { _isRunning = false; SetStatus("ERROR: " + Short(ex.Message)); }
                finally { _isRunning = false; }
            });
        }

        // ── OpenBriefing ──────────────────────────────────────────────────────

        private void OpenBriefing()
        {
            try
            {
                if (!Directory.Exists(BriefingDir)) { SetStatus("ERROR: PDF folder not found: " + BriefingDir); return; }
                var pdfs = Directory.GetFiles(BriefingDir, "briefing_ES_*.pdf").OrderByDescending(f => f).ToArray();
                if (pdfs.Length == 0) { SetStatus("! No PDF found"); return; }
                Process.Start(new ProcessStartInfo(pdfs[0]) { UseShellExecute = true });
                SetStatus("PDF: " + Path.GetFileName(pdfs[0]));
            }
            catch (Exception ex) { SetStatus("ERROR: " + Short(ex.Message)); }
        }

        public override void Dispose()
        {
            // Kill the intraday loop process if still running
            try
            {
                if (_loopProcess != null && !_loopProcess.HasExited)
                {
                    try { _loopProcess.Kill(entireProcessTree: true); } catch { }
                    _loopProcess.WaitForExit(1000);
                }
            }
            catch { }
            _loopProcess = null;
            _loopRunning = false;
            try { RestoreChartOverlay(); } catch { }
            base.Dispose();
        }

        // ── Intraday loop refresh: background process toggle ─────────────────
        private void ToggleLoop()
        {
            if (_loopRunning) StopLoop();
            else              StartLoop();
        }

        private void StartLoop()
        {
            if (_loopRunning) return;
            if (!File.Exists(IntradayRefreshScriptPath)) { SetStatus("ERROR: Intraday script not found: " + IntradayRefreshScriptPath); return; }
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = OfkUtils.ResolveExe(PythonExePath),
                    Arguments              = "\"" + IntradayRefreshScriptPath + "\" ES --loop",
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                    WindowStyle            = ProcessWindowStyle.Hidden,
                    WorkingDirectory       = System.IO.Path.GetDirectoryName(IntradayRefreshScriptPath)
                                             ?? OfkEnv.Pipeline,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                };
                // Force UTF-8 on Python stdout/stderr: otherwise box-drawing
                // chars raise UnicodeEncodeError on cp1252 when piped.
                psi.Environment["PYTHONIOENCODING"] = "utf-8";
                psi.Environment["PYTHONUTF8"]       = "1";
                _loopProcess = Process.Start(psi);
                if (_loopProcess == null) { SetStatus("ERROR: Loop startup failed"); return; }
                // Consume streams async: a full pipe buffer freezes the Python process.
                _loopProcess.OutputDataReceived += (s, e) => { /* discard */ };
                _loopProcess.ErrorDataReceived  += (s, e) => { /* discard */ };
                try { _loopProcess.BeginOutputReadLine(); } catch { }
                try { _loopProcess.BeginErrorReadLine();  } catch { }
                _loopRunning = true;
                _loopStartedAt = DateTime.Now;
                var proc = _loopProcess;
                proc.EnableRaisingEvents = true;
                proc.Exited += (s, e) =>
                {
                    if (!ReferenceEquals(_loopProcess, proc)) return; // stopped/replaced already
                    _loopRunning = false;
                    _loopProcess = null;
                    SetStatus("■ Intraday loop has stopped");
                };
                SetStatus($"● Intraday loop ON  (PID {_loopProcess.Id})");
            }
            catch (Exception ex) { SetStatus("ERROR: " + Short(ex.Message)); }
        }

        private void StopLoop()
        {
            try
            {
                if (_loopProcess != null && !_loopProcess.HasExited)
                {
                    try { _loopProcess.Kill(entireProcessTree: true); } catch { }
                    _loopProcess.WaitForExit(2000);
                }
            }
            catch { }
            _loopProcess = null;
            _loopRunning = false;
            SetStatus("■ Intraday loop OFF");
        }

        // ── Replay: step through today's 5-min snapshots (◀ older, ▶ newer) ──
        private void ReplayStep(int dir)
        {
            if (!_replayMode)
            {
                if (dir > 0) return; // already live
                _replaySnaps = GexLoader.ListSnapshots(IntradayHistoryDir, "ES", OfkEnv.NowEt.Date);
                if (_replaySnaps.Count == 0) { SetStatus("! No intraday snapshot today — turn the intraday loop ON"); return; }
                _replayIdx = _replaySnaps.Count - 1;
            }
            else
            {
                int next = _replayIdx + dir;
                if (next >= _replaySnaps.Count) { ExitReplayMode(); SetStatus("Live"); return; }
                _replayIdx = Math.Max(0, next);
            }
            var snap = _replaySnaps[_replayIdx];
            LoadReplaySnapshot(snap.Path, snap.Timestamp);
            SetStatus($"REPLAY snapshot {_replayIdx + 1}/{_replaySnaps.Count}  •  ► past the last = live");
        }

        // ── Chart rendering ──────────────────────────────────────────────────

        protected override void OnRender(RenderContext context, DrawingLayouts layout)
        {
            var lv = _levels;
            if (ChartInfo == null) return;
            if (!lv.Loaded) { if ((layout & DrawingLayouts.Final) != 0) DrawPanel(context); return; }
            int chartW = ChartArea.Width;

            // Zone pinning
            if (ShowPinZone && lv.GexRegime > 0 && lv.CallWall > 0 && lv.PutWall > 0)
            {
                int yCw = (int)ChartInfo.GetYByPrice((decimal)lv.CallWall, false);
                int yPw = (int)ChartInfo.GetYByPrice((decimal)lv.PutWall,  false);
                if (yCw < yPw) context.FillRectangle(DrawingColor.FromArgb(PinZoneOpacity * 255 / 100, 0, 200, 0), new Rectangle(0, yCw, chartW, yPw - yCw));
            }

            // Expected Move band (TastyTrade-style if EM TT available in pipeline)
            if (ShowEMZone && lv.ExpectedMoveHigh > 0 && lv.ExpectedMoveLow > 0)
            {
                int yH = (int)ChartInfo.GetYByPrice((decimal)lv.ExpectedMoveHigh, false);
                int yL = (int)ChartInfo.GetYByPrice((decimal)lv.ExpectedMoveLow,  false);
                int alphaEM = Math.Max(2, Math.Min(30, EMBandOpacity)) * 255 / 100;
                if (yH < yL)
                    context.FillRectangle(
                        DrawingColor.FromArgb(alphaEM, EMBandColor.R, EMBandColor.G, EMBandColor.B),
                        new Rectangle(0, yH, chartW, yL - yH));
            }

            // ─── Gamma Zones (Phase 1, TanukiTrade-style) ──────────────────────
            // Bounds: Put Wall ID < pTrans < cTrans < Call Wall ID
            // - Above Call Wall          = bullish squeeze (yellow)
            // - cTrans → Call Wall       = positive gamma (green)
            // - pTrans → cTrans          = transition (gray)
            // - Put Wall → pTrans        = negative gamma (red)
            // - Below Put Wall           = bearish squeeze (yellow)
            if (ShowGammaZones)
            {
                int alpha = Math.Max(2, Math.Min(40, GammaZoneOpacity)) * 255 / 100;
                double cw = lv.CallWallIntraday;
                double pw = lv.PutWallIntraday;
                double ct = lv.CTransIntraday;
                double pt = lv.PTransIntraday;
                int chartH = ChartArea.Height;

                // Helper: clamp Y to the visible zone
                int Clamp(double price)
                {
                    int y = (int)ChartInfo.GetYByPrice((decimal)price, false);
                    if (y < 0) y = 0;
                    if (y > chartH) y = chartH;
                    return y;
                }

                // Bullish squeeze: above Call Wall (top of chart to yCw)
                if (cw > 0)
                {
                    int yCw = Clamp(cw);
                    if (yCw > 0)
                        context.FillRectangle(
                            DrawingColor.FromArgb(alpha, ZoneSqueezeColor.R, ZoneSqueezeColor.G, ZoneSqueezeColor.B),
                            new Rectangle(0, 0, chartW, yCw));
                }

                // Positive gamma: between cTrans and Call Wall
                if (cw > 0 && ct > 0 && ct < cw)
                {
                    int yCw = Clamp(cw);
                    int yCt = Clamp(ct);
                    if (yCt > yCw)
                        context.FillRectangle(
                            DrawingColor.FromArgb(alpha, ZonePositiveColor.R, ZonePositiveColor.G, ZonePositiveColor.B),
                            new Rectangle(0, yCw, chartW, yCt - yCw));
                }

                // Transition: between pTrans and cTrans
                if (ct > 0 && pt > 0 && pt < ct)
                {
                    int yCt = Clamp(ct);
                    int yPt = Clamp(pt);
                    if (yPt > yCt)
                        context.FillRectangle(
                            DrawingColor.FromArgb(alpha, ZoneTransitionColor.R, ZoneTransitionColor.G, ZoneTransitionColor.B),
                            new Rectangle(0, yCt, chartW, yPt - yCt));
                }

                // Negative gamma: between Put Wall and pTrans
                if (pw > 0 && pt > 0 && pw < pt)
                {
                    int yPt = Clamp(pt);
                    int yPw = Clamp(pw);
                    if (yPw > yPt)
                        context.FillRectangle(
                            DrawingColor.FromArgb(alpha, ZoneNegativeColor.R, ZoneNegativeColor.G, ZoneNegativeColor.B),
                            new Rectangle(0, yPt, chartW, yPw - yPt));
                }

                // Bearish squeeze: below Put Wall (from yPw to bottom of chart)
                if (pw > 0)
                {
                    int yPw = Clamp(pw);
                    if (yPw < chartH)
                        context.FillRectangle(
                            DrawingColor.FromArgb(alpha, ZoneSqueezeColor.R, ZoneSqueezeColor.G, ZoneSqueezeColor.B),
                            new Rectangle(0, yPw, chartW, chartH - yPw));
                }
            }

            var penGF   = new RenderPen(GammaFlipColor,        LineWidth + 1);
            var penVT   = new RenderPen(VolTriggerColor,       LineWidth);
            var penCW   = new RenderPen(CallWallColor,         LineWidth);
            var penPW   = new RenderPen(PutWallColor,          LineWidth);
            var penRP   = new RenderPen(RiskPivotColor,        LineWidth);
            var penVF   = new RenderPen(VannaFlipColor,        1);
            var penCM   = new RenderPen(CharmMagnetColor,      1);
            var penMP   = new RenderPen(MaxPainColor,          LineWidth);
            var penEMH  = new RenderPen(ExpectedMoveHighColor, 1);
            var penEML  = new RenderPen(ExpectedMoveLowColor,  1);
            var penOI1  = new RenderPen(TopOI1Color,           1);
            var penOI2  = new RenderPen(TopOI2Color,           1);
            var penOI3  = new RenderPen(TopOI3Color,           1);
            var penCWid = new RenderPen(CallWallIntradayColor, LineWidth + 1);
            var penPWid = new RenderPen(PutWallIntradayColor,  LineWidth + 1);
            var penOIid1= new RenderPen(TopOIIntraday1Color,   2);
            var penOIid2= new RenderPen(TopOIIntraday2Color,   2);
            var penOIid3= new RenderPen(TopOIIntraday3Color,   2);

            DrawLevel(context, chartW, lv.GammaFlip,        ShowGammaFlip,        GammaFlipColor,        penGF,  10, 5, $"Gamma Flip  {lv.GammaFlip:F0}  [GEX {lv.GexTotal/1e9:+0.000;-0.000}B]");
            DrawLevel(context, chartW, lv.VolTrigger,       ShowVolTrigger,       VolTriggerColor,       penVT,  8,  4, $"Vol Trigger  {lv.VolTrigger:F0}");
            DrawLevel(context, chartW, lv.CallWall,         ShowCallWall,         CallWallColor,         penCW,  8,  4, $"Call Wall  {lv.CallWall:F0}  [GEX {lv.CallWallGex/1e9:+0.000;-0.000}B]");
            DrawLevel(context, chartW, lv.PutWall,          ShowPutWall,          PutWallColor,          penPW,  8,  4, $"Put Wall  {lv.PutWall:F0}  [GEX {lv.PutWallGex/1e9:+0.000;-0.000}B]");
            DrawLevel(context, chartW, lv.RiskPivot,        ShowRiskPivot,        RiskPivotColor,        penRP,  10, 5, $"Risk Pivot  {lv.RiskPivot:F0}");
            DrawLevel(context, chartW, lv.VannaFlip,        ShowVannaFlip,        VannaFlipColor,        penVF,  2,  4, $"Vanna Flip  {lv.VannaFlip:F0}  [VEX {lv.VexTotal/1e8:+0.00;-0.00}]");
            DrawLevel(context, chartW, lv.CharmMagnet,      ShowCharmMagnet,      CharmMagnetColor,      penCM,  2,  4, $"Charm  {lv.CharmMagnet:F0}");
            DrawLevel(context, chartW, lv.MaxPain,          ShowMaxPain,          MaxPainColor,          penMP,  6,  4, $"Max Pain  {lv.MaxPain:F0}");
            DrawLevel(context, chartW, lv.ExpectedMoveHigh, ShowExpectedMoveHigh, ExpectedMoveHighColor, penEMH, 4,  6, $"EM High  {lv.ExpectedMoveHigh:F0}");
            DrawLevel(context, chartW, lv.ExpectedMoveLow,  ShowExpectedMoveLow,  ExpectedMoveLowColor,  penEML, 4,  6, $"EM Low  {lv.ExpectedMoveLow:F0}");
            DrawLevel(context, chartW, lv.TopOI1,           ShowTopOI1,           TopOI1Color,           penOI1, 3,  6, $"OI #1  {lv.TopOI1:F0}  ({lv.TopOI1Vol:N0})");
            DrawLevel(context, chartW, lv.TopOI2,           ShowTopOI2,           TopOI2Color,           penOI2, 3,  6, $"OI #2  {lv.TopOI2:F0}  ({lv.TopOI2Vol:N0})");
            DrawLevel(context, chartW, lv.TopOI3,           ShowTopOI3,           TopOI3Color,           penOI3, 3,  6, $"OI #3  {lv.TopOI3:F0}  ({lv.TopOI3Vol:N0})");
            // INTRADAY levels (CBOE 0-7 DTE — scalping primary)
            string idTag = $" [0-{(lv.WallsIntradayMaxDte > 0 ? lv.WallsIntradayMaxDte : 7)}d]";
            DrawLevel(context, chartW, lv.CallWallIntraday, ShowCallWallIntraday, CallWallIntradayColor, penCWid, 9, 4, $"CW ID  {lv.CallWallIntraday:F0}  [GEX {lv.CallWallIntradayGex/1e9:+0.000;-0.000}B]" + idTag);
            DrawLevel(context, chartW, lv.PutWallIntraday,  ShowPutWallIntraday,  PutWallIntradayColor,  penPWid, 9, 4, $"PW ID  {lv.PutWallIntraday:F0}  [GEX {lv.PutWallIntradayGex/1e9:+0.000;-0.000}B]" + idTag);
            DrawLevel(context, chartW, lv.TopOIIntraday1,   ShowTopOIIntraday1,   TopOIIntraday1Color,   penOIid1, 3, 6, $"OI ID #1  {lv.TopOIIntraday1:F0}  ({lv.TopOIIntraday1Vol:N0})");
            DrawLevel(context, chartW, lv.TopOIIntraday2,   ShowTopOIIntraday2,   TopOIIntraday2Color,   penOIid2, 3, 6, $"OI ID #2  {lv.TopOIIntraday2:F0}  ({lv.TopOIIntraday2Vol:N0})");
            DrawLevel(context, chartW, lv.TopOIIntraday3,   ShowTopOIIntraday3,   TopOIIntraday3Color,   penOIid3, 3, 6, $"OI ID #3  {lv.TopOIIntraday3:F0}  ({lv.TopOIIntraday3Vol:N0})");
            // Transition levels cTrans/pTrans (Phase 3 — TanukiTrade-style)
            var penCT = new RenderPen(CTransIntradayColor, LineWidth);
            var penPT = new RenderPen(PTransIntradayColor, LineWidth);
            DrawLevel(context, chartW, lv.CTransIntraday,   ShowCTransIntraday,   CTransIntradayColor,   penCT, 3, 8, $"cTrans  {lv.CTransIntraday:F0}  [call dom.]" + idTag);
            DrawLevel(context, chartW, lv.PTransIntraday,   ShowPTransIntraday,   PTransIntradayColor,   penPT, 3, 8, $"pTrans  {lv.PTransIntraday:F0}  [put dom.]" + idTag);
            // DEX D+ / D- (Phase 4 — TanukiTrade-style, directional hedging pressure)
            var penDp = new RenderPen(DexPlusIntradayColor,  LineWidth);
            var penDm = new RenderPen(DexMinusIntradayColor, LineWidth);
            DrawLevel(context, chartW, lv.DexPlusIntraday,  ShowDexPlusIntraday,  DexPlusIntradayColor,  penDp, 2, 5, $"D+  {lv.DexPlusIntraday:F0}  [DEX {lv.DexPlusIntradayDex/1e6:+0.00;-0.00}M]" + idTag);
            DrawLevel(context, chartW, lv.DexMinusIntraday, ShowDexMinusIntraday, DexMinusIntradayColor, penDm, 2, 5, $"D-  {lv.DexMinusIntraday:F0}  [DEX {lv.DexMinusIntradayDex/1e6:+0.00;-0.00}M]" + idTag);
            // Abs GEX Ab1/Ab2/Ab3 (Phase 5 — pin risk, concentrated absolute gamma)
            var penAb1 = new RenderPen(AbsGex1Color, 1);
            var penAb2 = new RenderPen(AbsGex2Color, 1);
            var penAb3 = new RenderPen(AbsGex3Color, 1);
            DrawLevel(context, chartW, lv.AbsGex1, ShowAbsGex1, AbsGex1Color, penAb1, 2, 8, $"Ab1  {lv.AbsGex1:F0}  [|GEX| {lv.AbsGex1Gex/1e9:+0.000;-0.000}B]" + idTag);
            DrawLevel(context, chartW, lv.AbsGex2, ShowAbsGex2, AbsGex2Color, penAb2, 2, 8, $"Ab2  {lv.AbsGex2:F0}  [|GEX| {lv.AbsGex2Gex/1e9:+0.000;-0.000}B]" + idTag);
            DrawLevel(context, chartW, lv.AbsGex3, ShowAbsGex3, AbsGex3Color, penAb3, 2, 8, $"Ab3  {lv.AbsGex3:F0}  [|GEX| {lv.AbsGex3Gex/1e9:+0.000;-0.000}B]" + idTag);
            // Extended walls (Phase 6 — TanukiTrade GEX7-10), color depends on call/put side
            DrawingColor extColor(string side) => side == "put" ? GexExtPutColor : GexExtCallColor;
            void DrawExt(int n, double price, bool show, double gex, string side)
            {
                var col = extColor(side);
                var pen = new RenderPen(col, 1);
                string sideTag = side == "put" ? "P" : "C";
                DrawLevel(context, chartW, price, show, col, pen, 2, 10,
                    $"GEX#{6+n}{sideTag}  {price:F0}  [{gex/1e9:+0.000;-0.000}B]" + idTag);
            }
            DrawExt(1, lv.GexExt1, ShowGexExt1, lv.GexExt1Gex, lv.GexExt1Side);
            DrawExt(2, lv.GexExt2, ShowGexExt2, lv.GexExt2Gex, lv.GexExt2Side);
            DrawExt(3, lv.GexExt3, ShowGexExt3, lv.GexExt3Gex, lv.GexExt3Side);
            DrawExt(4, lv.GexExt4, ShowGexExt4, lv.GexExt4Gex, lv.GexExt4Side);
            // 0DTE levels (Phase C)
            var penMP0  = new RenderPen(MaxPain0DTEColor,     1);
            var penPin0 = new RenderPen(PinStrike0DTEColor,   LineWidth + 1);
            var penChM0 = new RenderPen(CharmMagnet0DTEColor, LineWidth);
            string zd = lv.ZeroDTEDte == 0 ? "0DTE" : $"{lv.ZeroDTEDte}DTE";
            DrawLevel(context, chartW, lv.MaxPain0DTE,     ShowMaxPain0DTE,     MaxPain0DTEColor,     penMP0,  6, 4, $"Max Pain {zd}  {lv.MaxPain0DTE:F0}");
            DrawLevel(context, chartW, lv.PinStrike0DTE,   ShowPinStrike0DTE,   PinStrike0DTEColor,   penPin0, 5, 3, $"Pin {zd}  {lv.PinStrike0DTE:F0}");
            DrawLevel(context, chartW, lv.CharmMagnet0DTE, ShowCharmMagnet0DTE, CharmMagnet0DTEColor, penChM0, 4, 3, $"Charm {zd}  {lv.CharmMagnet0DTE:F0}");

            // ─── On-chart alert banners ───────────────────────────────────
            if (EnableVisualBanners)
            {
                BannerEntry[] active;
                lock (_bannerSync)
                {
                    _banners.RemoveAll(b => (DateTime.Now - b.Time).TotalSeconds > BannerDurationSeconds);
                    active = _banners.ToArray();
                }
                if (active.Length > 0)
                {
                    var bFont = new RenderFont("Arial", 9);
                    int count = Math.Min(active.Length, MaxVisibleBanners);
                    int bH = 22, bGap = 3, bTop = 8, bRight = 10;

                    // Dynamic detection of useful right edge:
                    // if the DOM trader is open, ChartArea.Width includes its
                    // width. We use the X position of the last drawn bar
                    // + a small margin as the effective right edge —
                    // the DOM trader always lives after this area.
                    int rightEdge = chartW;
                    try
                    {
                        if (CurrentBar > 0)
                        {
                            int lastBarX = (int)ChartInfo.GetXByBar(CurrentBar - 1);
                            if (lastBarX > 0 && lastBarX + 20 < chartW)
                                rightEdge = lastBarX + 20;
                        }
                    }
                    catch { }

                    for (int i = 0; i < count; i++)
                    {
                        var entry = active[active.Length - 1 - i];
                        int remain = Math.Max(0, BannerDurationSeconds - (int)(DateTime.Now - entry.Time).TotalSeconds);
                        string txt = $"  {entry.Message}  ({remain}s)";
                        var ts = context.MeasureString(txt, bFont);
                        int bW = Math.Min((int)ts.Width + 20, (int)(rightEdge * 0.7));
                        int bx = rightEdge - bW - bRight;
                        int by = bTop + i * (bH + bGap);
                        float fade = Math.Min(1f, remain / 5f);
                        int bgA = (int)(215 * fade);
                        int fgA = (int)(240 * fade);
                        int strA = (int)(255 * fade);
                        context.FillRectangle(DrawingColor.FromArgb(bgA, 15, 15, 20), new Rectangle(bx, by, bW, bH));
                        context.FillRectangle(DrawingColor.FromArgb(strA, entry.Color.R, entry.Color.G, entry.Color.B), new Rectangle(bx, by, 4, bH));
                        context.DrawString(txt, bFont, DrawingColor.FromArgb(fgA, 220, 220, 230), bx + 8, by + 3);
                    }
                }
            }

            // ─── REPLAY overlay (centered at top of chart) ─────────────────
            if (_replayMode)
            {
                var rFont = new RenderFont("Arial Bold", 13);
                string rTxt = $"REPLAY  {_replayTimestamp:HH:mm}";
                var rs = context.MeasureString(rTxt, rFont);
                int rW = (int)rs.Width + 20;
                int rH = (int)rs.Height + 8;
                int rx = (chartW - rW) / 2;
                int ry = 8;
                context.FillRectangle(DrawingColor.FromArgb(220, 60, 40, 90), new Rectangle(rx, ry, rW, rH));
                context.FillRectangle(DrawingColor.FromArgb(255, 189, 147, 249), new Rectangle(rx, ry, rW, 2));
                context.DrawString(rTxt, rFont, DrawingColor.FromArgb(255, 230, 220, 250), rx + 10, ry + 4);
            }

            if ((layout & DrawingLayouts.Final) != 0) DrawPanel(context);
        }

        // Dynamic font (LabelFontSize was hard-coded to 9 — bug fix)
        private RenderFont GetLabelFont() => new RenderFont("Arial", Math.Max(7, Math.Min(14, LabelFontSize)));

        private void DrawLevel(RenderContext ctx, int chartW, double price, bool show,
                                DrawingColor color, RenderPen pen, int dashLen, int gapLen, string label)
        {
            if (!show || price <= 0) return;
            int y = (int)ChartInfo.GetYByPrice((decimal)price, false);
            if (y < 0 || y > ChartArea.Height + 200) return;

            // Line (option: right-side extension only, from last bar)
            int xStart = 0;
            if (LineExtensionRightOnly && CurrentBar > 0)
            {
                int xLast = (int)ChartInfo.GetXByBar(CurrentBar - 1);
                xStart = Math.Max(0, xLast);
            }

            if (UseSolidLines)
            {
                ctx.DrawLine(pen, xStart, y, chartW, y);
            }
            else
            {
                int x = xStart; bool drawing = true;
                while (x < chartW)
                {
                    int end = Math.Min(x + (drawing ? dashLen : gapLen), chartW);
                    if (drawing) ctx.DrawLine(pen, x, y, end, y);
                    x = end; drawing = !drawing;
                }
            }

            // Label (option: on/off + left/right position + background opacity)
            if (!ShowLineLabels) return;
            var font = GetLabelFont();
            var ts = ctx.MeasureString(label, font);
            int lw = (int)ts.Width + 12, lh = (int)ts.Height + 4;
            int lx = LabelOnRight ? chartW - lw - 8 : 6;
            int ly = y - (int)ts.Height - 6;
            int bgAlpha = Math.Max(20, Math.Min(100, LabelBgOpacity)) * 255 / 100;
            ctx.FillRectangle(DrawingColor.FromArgb(bgAlpha, 10, 10, 10),               new Rectangle(lx-2, ly-1, lw, lh));
            ctx.FillRectangle(DrawingColor.FromArgb(220, color.R, color.G, color.B),    new Rectangle(lx-2, ly-1, 3,  lh));
            ctx.DrawString(label, font, DrawingColor.FromArgb(255, color.R, color.G, color.B), lx+4, ly+2);
        }

        // ── LoadLevels (delegates to shared loader) ──────────────────────────
        private void LoadLevels()
        {
            if (_replayMode) return; // freeze during replay
            var (gex, meta, ok) = GexLoader.Load(JsonPath, "es");
            // Failed read (e.g. caught mid-write by the pipeline): keep the last
            // good levels; _jsonSeen is unchanged, so the next check retries.
            if (!ok) return;
            _levels       = gex;
            _meta         = meta;
            _loadedDate   = OfkEnv.NowEt.ToString("yyyy-MM-dd");
            _lastLoadTime = DateTime.Now;
            _jsonSeen     = OfkUtils.LastWriteUtc(JsonPath);
            _levelsLoaded = true;
            LoadAlertStats();
            UpdatePanelText();
        }

        // ── Intraday replay: loading a historical snapshot ───────────────────
        public void LoadReplaySnapshot(string snapshotPath, DateTime timestamp)
        {
            var (gex, meta, ok) = GexLoader.LoadSnapshotPath(snapshotPath, "es");
            if (!ok) return;
            _replayMode      = true;
            _replayTimestamp = timestamp;
            _levels          = gex;
            _meta            = meta;
            _levelsLoaded    = true;
            UpdatePanelText();
            try { RedrawChart(); } catch { }
        }

        public void ExitReplayMode()
        {
            _replayMode      = false;
            _replayIdx       = -1;
            _replayTimestamp = DateTime.MinValue;
            LoadLevels();
            UpdatePanelText();
            try { RedrawChart(); } catch { }
        }

        public override string ToString() => "OFK ES GEX Levels";
    }
}

using System;
namespace Starship.Physics;
public static class Const {
    public const double G0 = 9.80665, RE = 6371e3, MU = 3.986004418e14, WE = 7.292115e-5, P0 = 101325,
        RAIR = 287.053, D2R = Math.PI / 180.0, R2D = 180.0 / Math.PI, LAT = 25.997 * D2R;
    public static readonly double W = WE * Math.Cos(LAT);
    public const double CATCH_H = 62, ARM_PARK = 116, ARM_LOWER = 1.2, ARM_HAUL = 420, CATCH_DR = 8,
        CATCH_WIN = 14, CATCH_VV = 4.0, CATCH_VH = 3.5, CATCH_TILT = 6, CATCH_TILT_CMD = 5, LAND_VTD = 1.1;
    public const double ARM_GAP_PARK = 14, ARM_GAP_READY = 10, ARM_GAP_RATE = 3, ARM_APPROACH_H = 8000;
    public static double ARM_MOVE_V = 2, ARM_MOVE_A = 1, ARM_MOVE_W = 0.8, CATCH_RAIL = 0.5, CATCH_OM = 3;
    public const double ARM_SAG_REST = 0.5, ARM_SAG_PERIOD = 1.2, ARM_SAG_ZETA = 0.75, ARM_HOLD_T = 4,
        ARM_SLIDE_TAU = 0.35, ARM_TILT_PERIOD = 1.2, ARM_TILT_ZETA = 0.8;
    public static double LAND_ALAT = 4.0;
    public static double LAND_TLAG = 2.5, LAND_KLAT = 0.6, LAND_DHPD = 650, LAND_TILT_NEAR = 16;
    public static double LAND_KDAMP = 2.4, LAND_TILT_END = 7, LAND_DH_END = 18;
    public static double LAND_POLE = 0.43, LAND_B_GMAX = 6;
    public const double LAND_CUT2 = 9, LAND_CUT_HOLD = 0.8;
    public const int LAND_B_END = 3;
    public static double LAND_THR_MIN = 0.40, LAND_PROJ = 0.5;
    public static double LAND_WIND_K = 2.0, LAND_WIND_H = 3000;
    public const double DECK_H = 16, MOUNT_R = 23, COAST_DR = 700, LAND_W = 30000, SEA_DR = 6000;
    public static double GO_MISS_B = 1500, GO_PROP_B = 75e3, GO_PROP_S = 25e3, GO_DMG_S = 0.15, GO_WIND = 20,
        GO_POLL_H = 3000, WAIT_MAX = 12, TIP_K = 0.4, REACH_S = 500, REACH_B = 700, RELIGHT_P = 0.02;
    public static double ASC_CLEAR_H = 87, ASC_Q_IN = 25.5e3, ASC_Q_OUT = 24.5e3,
        HOT_IGN_F = 0.15, HOT_IGN_T = 1.5, HOT_PC = 0.85, HOT_SEP_T = 3.5;
    public static double NAV_LAG = 0.1, NAV_POS_SIG = 0.8, NAV_POS_TAU = 20, NAV_VEL_SIG = 0.08, NAV_VEL_TAU = 4,
        NAV_OBS_DT = 0.05, NAV_OBS_H = 5000, NAV_OBS_SMIN = 2e-3, NAV_WIND_TAU = 1.5,
        NAV_RHO_H = 60e3, NAV_RHO_Q = 2000, NAV_RHO_TAU = 3;
    public const double TILE_CAP = 6000, SKIN_CAP = 15800, BODY_CAP = 160e3, TILE_LIMIT = 1700,
        SKIN_LIMIT = 1100, TILE_EPS = 0.85, SKIN_EPS = 0.45, LEE_SHADE = 0.03, BURN_RATE = 1.2e-6,
        ENTRY_BANK0 = 45;
    public static double ENTRY_KB = 6e-5, ENTRY_GAIN = 0.5, ENTRY_KT = 5e3, ENTRY_TRIM_LO = -18, ENTRY_TRIM_HI = 8;
    public const double BANK_RATE = 8, BANK_SMOOTH = 0.35, ALPHA_RATE = 3.0, ENTRY_PRED_DT = 0.5;
    public static double FLIP_H = 1500, HOLD_MAX = 2, LAND_STOP_S = 500;
    public const double SEP_GAP = 2.35;
    public static double HDR_Z = 0.82, CP_BELLY = 0.059, CP_BELLY_M0 = 0.8, CP_BELLY_M1 = 1.5;
    public const double BAY_Z0 = 21.6, BAY_Z1 = 35.2, PRED_H_MAX = 2000e3, PRED_T_MAX = 12000,
        GLIDE_H = 25000, ENTRY_K0 = 1.08;
    public static double ENTRY_PROP = 45e3;
    public static double FLIP_STOP = 650, FLIP_H_SEA = 700;
    public static double OM_ACC_MAX = 0.30, OM_ACC_FLIP = 1.0, GIM_RATE = 30, OM_BRAKE = 0.7, OM_VAC = 1.5, Q_VAC = 100;
    public static double SETTLE_A = 0.005, UNSETTLE_T = 0.4, SETTLE_TMIN = 0.3, SETTLE_TMAX = 120,
        SETTLE_GO = 0.9, SETTLE_TRIP = 0.5, GAS_TRIP_T = 0.3, ULLAGE_OFF = 1e5, ULLAGE_A = 0.02, SLOSH_ZETA = 0.03, SLOSH_MAX = 0.5;
    public static double FLIP_BRAKE = 0.5, FLIP_KW = 4, FLIP_TW = 2.4, SHIP_FLIP_TW = 1.5, FLIP_END = 10, FLIP_OM_END = 8, FLIP_D = 200;
    public static double SHIP_ALAT = 6, SHIP_TILT = 25, SHIP_GATE = 40, SHIP_AD = 6, SHIP_AB = 9,
        SHIP_ENG_K = 1.3, SHIP_POLE = 0.9, SHIP_KP = 6, SHIP_KD = 4.5, BOOST_KP = 0.9, BOOST_KD = 2.6, SHIP_CATCH_V = 2, SHIP_FINAL_A = 2.5, SHIP_KV = 1.5,
        SHIP_HOLD_DR = 4, SHIP_HOLD_VH = 2, SHIP_HOLD_H = 15, SHIP_WAIT_T = 5;
    public static double SECO_LEAD = 1.8, SECO_PERI = -800e3;
    public static double CIRC_TAPER = 150e3, CIRC_ONE = 80e3;
    public const double FLAP_S_FWD = 19.9, FLAP_S_AFT = 36.7;
    public const double FLAP_Y_FWD = 47.64, FLAP_Y_AFT = 5.91;
    public static double FLAP_FWD_MAX = 85, FLAP_AFT_MAX = 85, FLAP_RATE = 26, FLAP_SPAN = 85;
    public const double FIN_S = 37.4, FIN_Y = 61.2, FIN_N = 2;
    public static double FIN_RATE = 20, FIN_DEF = 25, FIN_CD = 0.2;
    public static double FIN_CN = 1.5;
    public static double GLIDE_KA = 400, GLIDE_A_LO = 45, GLIDE_A_HI = 70;
    public static double BELLY_TILT = 15, BELLY_VP0 = 150, BELLY_VP1 = 170, BELLY_LEAD = 1600, BELLY_VF = 0, BELLY_TMIN = 3;
    public static double BOOST_AOA = 17, BOOST_STRAIGHT_H = 4000, BOOST_SWITCH_H = 450;
    public static double ASC_KA = 5e-4, ASC_KV = 0.04, ASC_AVMAX = 40, ASC_THMAX = 140, DEO_AIM = 30e3, DEO_TURN = 1500e3;
    public const double DT = 0.01;
    public static double Clamp(double v, double a, double b) => v < a ? a : (v > b ? b : v);
    public static double Lerp(double a, double b, double t) => a + (b - a) * t;
}

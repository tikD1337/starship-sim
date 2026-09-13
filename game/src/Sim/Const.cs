using System;
namespace Starship.Physics;
public static class Const {
    public const double G0 = 9.80665, RE = 6371e3, MU = 3.986004418e14, WE = 7.292115e-5, P0 = 101325,
        RAIR = 287.053, D2R = Math.PI / 180.0, R2D = 180.0 / Math.PI, LAT = 25.997 * D2R;
    public static readonly double W = WE * Math.Cos(LAT);
    public const double CATCH_H = 62, ARM_PARK = 116, ARM_LOWER = 1.2, ARM_HAUL = 420, CATCH_DR = 8,
        CATCH_WIN = 14, CATCH_VV = 4.0, CATCH_VH = 3.5, CATCH_TILT = 6, LAND_VTD = 1.1;
    public static double LAND_ALAT = 4.0;
    public static double LAND_TLAG = 2.5, LAND_KLAT = 0.6, LAND_DHPD = 300, LAND_TILT_NEAR = 16;
    public static double LAND_KDAMP = 2.4, LAND_TILT_END = 7, LAND_DH_END = 18;
    public static double LAND_POLE = 0.55;
    public const double OM_DAMP = 0.15;
    public const double LAND_CUT2 = 9, LAND_CUT_HOLD = 0.8;
    public const int LAND_B_END = 3;
    public static double LAND_THR_MIN = 0.40, LAND_PROJ = 0.5;
    public static double LAND_WIND_K = 2.0, LAND_WIND_H = 3000;
    public const double TILE_CAP = 6000, SKIN_CAP = 15800, BODY_CAP = 160e3, TILE_LIMIT = 1700,
        SKIN_LIMIT = 1100, TILE_EPS = 0.85, SKIN_EPS = 0.45, LEE_SHADE = 0.03, BURN_RATE = 1.2e-6,
        ENTRY_BANK0 = 45;
    public static double ENTRY_KB = 6e-5, ENTRY_KT = 5e3, ENTRY_TRIM_LO = -18, ENTRY_TRIM_HI = 8;
    public const double BANK_RATE = 8, BANK_SMOOTH = 0.35, ALPHA_RATE = 3.0, ENTRY_PRED_DT = 0.5;
    public static double FLIP_H = 1500, HOLD_MAX = 2, LAND_STOP_S = 500;
    public const double SEP_GAP = 2.35;
    public static double HDR_Z = 0.82;
    public const double BAY_Z0 = 21.6, BAY_Z1 = 35.2, PRED_H_MAX = 2000e3, PRED_T_MAX = 12000,
        GLIDE_H = 25000, ENTRY_K0 = 1.08;
    public static double ENTRY_PROP = 60e3;
    public static double FLIP_STOP = 650, FLIP_H_SEA = 900;
    public static double OM_ACC_MAX = 0.30, OM_ACC_FLIP = 0.45;
    public static double SECO_LEAD = 1.8, SECO_PERI = -800e3;
    public static double CIRC_TAPER = 150e3, CIRC_ONE = 80e3;
    public const double FLAP_S_FWD = 19.9, FLAP_S_AFT = 36.7;
    public const double FLAP_Y_FWD = 47.64, FLAP_Y_AFT = 5.91;
    public static double FLAP_DEF = 20 * D2R;
    public const double FIN_S = 37.4, FIN_Y = 61.2;
    public static double FIN_CN = 1.5;
    public static double GLIDE_KA = 400, GLIDE_A_LO = 45, GLIDE_A_HI = 70;
    public static double FLIP_DRIFT_K = 0.8;
    public static double BOOST_AOA = 24, BOOST_STRAIGHT_H = 4000, BOOST_SWITCH_H = 450;
    public static double ASC_KA = 5e-4, ASC_KV = 0.04, ASC_AVMAX = 40, ASC_THMAX = 140, DEO_AIM = 30e3;
    public const double DT = 0.01;
    public static double Clamp(double v, double a, double b) => v < a ? a : (v > b ? b : v);
    public static double Lerp(double a, double b, double t) => a + (b - a) * t;
}

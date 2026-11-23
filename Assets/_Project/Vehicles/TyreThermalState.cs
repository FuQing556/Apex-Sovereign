using UnityEngine;

namespace Apex {
// One wheel. Heat capacities are J/K, conductances W/K, input powers W.
public struct TyreThermalState {
    public float TreadC { get; private set; }
    public float CarcassC { get; private set; }
    public float AirC { get; private set; }

    public TyreThermalState(float coldC) { TreadC = CarcassC = AirC = coldC; }

    public void Step(CompetitionSetup s, float slipPower, float rollingPower, bool loaded, float airSpeed, float dt) {
        if (dt <= 0) return;
        float treadToAir = s.treadAirConductance + s.treadAirPerSpeed * airSpeed;
        float treadToRoad = loaded ? s.treadRoadConductance : 0;
        float toCarcass = s.treadCarcassConductance;
        float toGas = s.carcassGasConductance;
        float carcassToAmbient = s.carcassHeatCapacity / s.carcassCoolingSeconds;

        // Backward Euler in temperature increments. Each internal heat flux leaves
        // one node and enters its neighbour. Eliminate the tread and gas rows first.
        float treadDiagonal = s.treadHeatCapacity / dt + treadToAir + treadToRoad + toCarcass;
        float gasDiagonal = s.gasHeatCapacity / dt + toGas;
        float carcassDiagonal = s.carcassHeatCapacity / dt + carcassToAmbient + toCarcass + toGas;
        float intoCarcass = toCarcass * (TreadC - CarcassC);
        float intoGas = toGas * (CarcassC - AirC);
        float treadW = s.heatToTyre * slipPower - intoCarcass
            + treadToAir * (s.ambientC - TreadC) + treadToRoad * (s.roadC - TreadC);
        float carcassW = rollingPower + intoCarcass - intoGas + carcassToAmbient * (s.ambientC - CarcassC);
        float carcassDelta = (carcassW + toCarcass * treadW / treadDiagonal + toGas * intoGas / gasDiagonal)
            / (carcassDiagonal - toCarcass * toCarcass / treadDiagonal - toGas * toGas / gasDiagonal);
        TreadC += (treadW + toCarcass * carcassDelta) / treadDiagonal;
        CarcassC += carcassDelta;
        AirC += (intoGas + toGas * carcassDelta) / gasDiagonal;
    }

    // Approximate the last physics step's force from its applied WheelCollider curves.
    // PhysX reports slip and normal load but does not expose resolved Fx/Fy here.
    static float CurveValue(WheelFrictionCurve curve, float slip) {
        float magnitude = Mathf.Abs(slip);
        if (magnitude < curve.extremumSlip) {
            float u = Mathf.InverseLerp(0, curve.extremumSlip, magnitude);
            // Hermite segment with finite initial slope and a horizontal tangent at peak.
            return curve.extremumValue * u * (1.5f - .5f * u * u);
        }
        float v = Mathf.InverseLerp(curve.extremumSlip, curve.asymptoteSlip, magnitude);
        return Mathf.SmoothStep(curve.extremumValue, curve.asymptoteValue, v);
    }

    public static float SlipPower(float load, WheelFrictionCurve longitudinal, WheelFrictionCurve lateral,
        float forwardSlip, float sideSlip, float forwardSlipSpeed, float sideSlipSpeed) {
        if (load <= 0) return 0;
        float x = CurveValue(longitudinal, forwardSlip);
        float y = CurveValue(lateral, sideSlip);
        float nx = longitudinal.extremumValue > 0 ? x / longitudinal.extremumValue : 0;
        float ny = lateral.extremumValue > 0 ? y / lateral.extremumValue : 0;
        float combined = Mathf.Max(1, Mathf.Sqrt(nx * nx + ny * ny));
        return load / combined * (x * longitudinal.stiffness * Mathf.Abs(forwardSlipSpeed)
            + y * lateral.stiffness * Mathf.Abs(sideSlipSpeed));
    }
}
}

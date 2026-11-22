using UnityEngine;
using GossipSDK.Core.XR;
using GossipSDK.Tracking;
using GossipSDK.Core;
using GossipSDK.XR;
using GossipSDK.Tracking.GameplayMetrics;

[DisallowMultipleComponent]
public class HandControllerTrackingComponent : MonoBehaviour
{
    [SerializeField] private float sampleInterval = 0.5f;
    private float timer;

    private void Start()
    {
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer < sampleInterval)
            return;
        timer = 0f;

        var provider = XRBootstrap.HandControllers;
        if (provider == null || !provider.IsSupported)
            return;

        if (provider.TryGetLeftPose(out var lPos, out var lRot))
        {
            Send("left",  lPos, lRot);
        }

        if (provider.TryGetRightPose(out var rPos, out var rRot))
        {
            Send("right", rPos, rRot);
        }
    }

    private void Send(string hand, Vector3 pos, Quaternion rot)
    {
        float elevation = 0f;

        /*
         * LA CABEZA SE PIDE AL MISMO PROVEEDOR QUE LA MANO.
         *
         * `pos` viene de InputDevices y esta en espacio de TRACKING. Leer la
         * cabeza de Camera.main la traia en espacio de MUNDO, asi que la resta
         * mezclaba dos sistemas y el desplazamiento del rig entraba entero en
         * el brazo horizontal.
         *
         * Medido el 22-11-2026 en Quest 3, con el rig en (0.371, 0.000, 2.004):
         * horiz salia de 1.6 a 2.1 m cuando la distancia real hombro-mano era
         * de 0.08 a 0.58 m, y el atan2 aplastaba el angulo contra cero. Con el
         * brazo levantado el campo decia 15.8 grados y eran 53.8.
         *
         * El eje Y coincidia por casualidad, porque ese rig tenia offset
         * vertical 0: por eso el brazo bajando salia bien y el brazo subiendo
         * no. Esa asimetria -suelo de -63, techo de +25 en 265 sesiones- es la
         * firma del fallo.
         *
         * El angulo es invariante a la traslacion y al giro en Y del rig, asi
         * que calcularlo entero en espacio de tracking da el mismo numero sin
         * depender de como este montado el equipo.
         */
        var headPose = XRBootstrap.HeadPose;
        Vector3 headPos;
        Quaternion headRot;
        if (headPose != null && headPose.TryGetPose(out headPos, out headRot))
        {
            // Hombro estimado: 0.20 m bajo los ojos (p50: 20.1 cm hombres / 18.5 cm mujeres)
            // y 0.19 m hacia el lado de la mano (media biacromial p50 / 2).
            float side = hand == "left" ? -1f : 1f;
            Vector3 headRight = headRot * Vector3.right;
            Vector3 flatRight = Vector3.ProjectOnPlane(headRight, Vector3.up);
            flatRight = flatRight.sqrMagnitude > 0.0001f ? flatRight.normalized : headRight;

            Vector3 shoulder = headPos - new Vector3(0f, 0.2f, 0f) + flatRight * (0.19f * side);
            Vector3 arm = pos - shoulder;
            float horiz = new Vector2(arm.x, arm.z).magnitude;
            elevation = Mathf.Atan2(arm.y, horiz) * Mathf.Rad2Deg;
        }
        Gossip.Instance?.HandControllerTracker?.CapSession(
            new HandControllerTracker.EntityData
            {
                // T-24, 23-sep-2026: Yaw y Roll salen del EntityData. Medido en los cuatro
                // repos -Front 1662/1662, Backend-Front 429/429, Backend-SDK 190/190,
                // CoreSDK main 161/161 y dev 160/160, 0 fallos-: su unico consumidor era
                // buildHandPositionAggExpr, que no llama nadie. El heatmap de Hand & Controller
                // usa otra coleccion (interactionimages), asi que esto no lo roza.
                Hand = hand, Pitch = rot.eulerAngles.x,
                HandElevation = elevation,
                TimestampUtc = System.DateTime.UtcNow.ToString("o")
            });
    }
}

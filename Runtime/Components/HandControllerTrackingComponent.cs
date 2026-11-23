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

    // Ultima pose aceptada de cada mano, para descartar el mando posado.
    private Vector3 ultimaPosIzq, ultimaPosDer;
    private Quaternion ultimaRotIzq, ultimaRotDer;
    private bool hayUltimaIzq, hayUltimaDer;

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

        if (provider.TryGetLeftPose(out var lPos, out var lRot) &&
            !PoseRepetida(ref hayUltimaIzq, ref ultimaPosIzq, ref ultimaRotIzq, lPos, lRot))
        {
            Send("left",  lPos, lRot);
        }

        if (provider.TryGetRightPose(out var rPos, out var rRot) &&
            !PoseRepetida(ref hayUltimaDer, ref ultimaPosDer, ref ultimaRotDer, rPos, rRot))
        {
            Send("right", rPos, rRot);
        }
    }

    /*
     * EL MANDO POSADO NO ES UN BRAZO.
     *
     * Un mando dejado en una mesa sigue trackeado -trackingState completo- y
     * quieto, y el SDK le atribuia al brazo del jugador la postura de un
     * objeto apoyado. En la corrida del 22-11-2026 el mando derecho estuvo
     * 11 de 69 muestras quieto a 2.7 m del hombro mientras el jugador movia
     * el otro brazo.
     *
     * Medido despues sobre el corpus entero, usando Pitch -que no lo toca el
     * bug de coordenadas de la 2.0.63- y rachas de 3 muestras iguales:
     *
     *   base          muestras   posadas   de las de brazo alto
     *   gossip_beta      7.730     24.8%                  23.5%
     *   gossip_dev      88.012     10.5%                  26.8%
     *   gossip_prod        932     11.1%                  20.3%
     *
     * Una de cada cuatro muestras de brazo alto era un objeto apoyado, y la
     * contaminacion es PEOR en la banda alta que en el total: un mando en una
     * mesa se queda en una orientacion que lee como alta y ahi sigue. En dev
     * la mediana por sesion es 0%, pero 49 de 241 pasan del 10% y las peores
     * llegan al 92%: no esta repartido, hay sesiones que son un objeto.
     *
     * Por que repetir la pose y no el alcance del brazo. La otra opcion era
     * descartar lo que estuviera a mas de un brazo del hombro, pero el hombro
     * es una ESTIMACION -0.20 m bajo los ojos- y con ese error un alcance
     * legitimo por encima de la cabeza se acerca al limite: el umbral tiraria
     * justo las posturas extremas, que son las que el detector existe para
     * ver. Esta guarda no tiene ese riesgo, porque una mano sostenida nunca
     * repite pose a 2 Hz.
     *
     * La cadencia no se rompe: a un brazo sostenido no se le descarta nada,
     * asi que la conversion de muestras a segundos de HandReachStrain sigue
     * valiendo donde importa.
     */
    private bool PoseRepetida(
        ref bool hay,
        ref Vector3 ultimaPos,
        ref Quaternion ultimaRot,
        Vector3 pos,
        Quaternion rot)
    {
        bool repetida = hay && pos == ultimaPos && rot == ultimaRot;
        hay = true;
        ultimaPos = pos;
        ultimaRot = rot;
        return repetida;
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
        Vector3 headPos = Vector3.zero;
        Quaternion headRot = Quaternion.identity;
        bool hayCabeza = headPose != null && headPose.TryGetPose(out headPos, out headRot);
        if (hayCabeza)
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

                // LA MEDIDA VA JUNTO A LA INTERPRETACION.
                //
                // HandElevation estima un hombro; estas seis son las dos poses
                // crudas en espacio de tracking. Con ellas, un fallo como el del
                // 22-11-2026 se ve desde Mongo sin tocar el casco. La cabeza va
                // null si el proveedor no responde, porque un 0 ahi no se
                // distingue de una cabeza en el origen del rig.
                PosX = pos.x,
                PosY = pos.y,
                PosZ = pos.z,
                HeadPosX = hayCabeza ? headPos.x : (float?)null,
                HeadPosY = hayCabeza ? headPos.y : (float?)null,
                HeadPosZ = hayCabeza ? headPos.z : (float?)null,

                TimestampUtc = System.DateTime.UtcNow.ToString("o")
            });
    }
}

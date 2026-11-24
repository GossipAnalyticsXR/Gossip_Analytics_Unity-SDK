using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace GossipSDK.Core.Utilities
{
    /// <summary>
    /// Cuantos jugadores hay en la partida, y si lo sabemos.
    ///
    /// Known es el campo que importa. Un Count de 1 con Known=false NO significa que hubiera
    /// un jugador: significa que nadie nos lo dijo. Guardarlos igual es el fallo que este
    /// resolver existe para cerrar.
    /// </summary>
    public struct NetworkPlayerCount
    {
        public bool Known;
        public int Count;

        /// <summary>Que capa de red lo dijo. Solo para diagnostico.</summary>
        public string Library;

        public static NetworkPlayerCount Unknown()
        {
            return new NetworkPlayerCount { Known = false, Count = 0, Library = null };
        }

        public static NetworkPlayerCount From(int count, string library)
        {
            return new NetworkPlayerCount { Known = true, Count = count, Library = library };
        }
    }

    /// <summary>
    /// Lee el numero de jugadores de la capa de red que haya en el proyecto, sin depender de
    /// ninguna: todo por reflexion, asi que el SDK compila igual con o sin ellas.
    ///
    /// Dos reglas que no se negocian:
    ///
    /// 1. Cada sonda va envuelta en try/catch. Estamos reflexionando contra versiones de
    ///    librerias que no conocemos: si una cambia una propiedad, esto devuelve Unknown, no
    ///    tira la app del cliente. Un SDK de telemetria nunca puede ser la causa de un crash.
    ///
    /// 2. Ante la duda, Unknown. En Netcode y en Mirror un cliente puro NO ve la lista de
    ///    conectados: solo la ve el servidor. Ahi devolvemos Unknown en vez de un numero a
    ///    medias, porque media verdad en una metrica es una mentira.
    /// </summary>
    public static class NetworkPlayerCountResolver
    {
        public delegate NetworkPlayerCount Probe();

        private static readonly List<Probe> _custom = new List<Probe>();

        /// <summary>
        /// Para stacks que no estan en la lista, o para uno propio. Se prueban antes que las
        /// sondas de serie: quien conoce su juego sabe mas que nosotros.
        ///
        /// Existe para que un integrador con una capa de red rara no tenga que heredar de un
        /// MonoBehaviour: le basta una lambda.
        /// </summary>
        public static void Register(Probe probe)
        {
            if (probe == null)
                return;

            _custom.Add(probe);
        }

        public static NetworkPlayerCount Resolve()
        {
            for (int i = 0; i < _custom.Count; i++)
            {
                NetworkPlayerCount r = Run(_custom[i]);
                if (r.Known)
                    return r;
            }

            NetworkPlayerCount pun = Run(ProbePhotonPun);
            if (pun.Known)
                return pun;

            NetworkPlayerCount ngo = Run(ProbeNetcodeForGameObjects);
            if (ngo.Known)
                return ngo;

            // Si el integrador registro una sonda propia, hay red aunque no la sepamos leer:
            // ese es el caso de la regla 2 y se queda en Unknown.
            if (_custom.Count == 0)
            {
                NetworkPlayerCount none = Run(ProbeNoNetworkingLibrary);
                if (none.Known)
                    return none;
            }

            return NetworkPlayerCount.Unknown();
        }

        private static NetworkPlayerCount Run(Probe probe)
        {
            try
            {
                return probe();
            }
            catch
            {
                // Ver regla 1: una sonda rota devuelve Unknown, no revienta al integrador.
                return NetworkPlayerCount.Unknown();
            }
        }

        /// <summary>
        /// No haber ninguna capa de red instalada NO es lo mismo que haberla y no saber
        /// leerla, y hasta hoy las dos cosas caian en Unknown. Una app sin Photon ni Netcode
        /// es de un jugador, y eso no es una suposicion como la que se retiro en la 2.0.8: el
        /// ensamblado no existe en el proceso.
        ///
        /// El empate vaciaba la card Multiplayer and One Player. ResolveSessionType devolvia
        /// null, el backend excluye los vacios al agrupar, y la card salia 0 y 0 aunque
        /// hubiera sesiones. Medido el 24-11-2026 en App VR / Beta: 2 sesiones el 23-nov y
        /// measuredSessions 0 de 2.
        ///
        /// CountSource no gana ningun valor nuevo: estas sesiones salen como network-auto,
        /// que ya existia. "no-network" se queda dentro del SDK como diagnostico.
        /// </summary>
        /// Marcadores de capa de red. Basta que UNO exista para que no afirmemos nada: hay
        /// red aunque no sepamos leer su lista, que es la regla 2.
        ///
        /// Limite honesto: esta lista no puede ser exhaustiva. Una capa propietaria que no
        /// este aqui y cuyo integrador no haya llamado a Register() se reportaria como de un
        /// jugador. Para eso existen Register(Probe) y SetSessionType(), y por eso la lista
        /// cubre los stacks de mercado en vez de solo los dos que el SDK sabe leer.
        private static readonly string[] _networkMarkers =
        {
            "Photon.Pun.PhotonNetwork",
            "Photon.Realtime.LoadBalancingClient",
            "Fusion.NetworkRunner",
            "Unity.Netcode.NetworkManager",
            "Unity.NetCode.NetworkStreamReceiveSystem",
            "Mirror.NetworkManager",
            "FishNet.InstanceFinder",
            "Normal.Realtime.Realtime",
            "Nakama.Client",
            "Colyseus.ColyseusClient"
        };

        private static NetworkPlayerCount ProbeNoNetworkingLibrary()
        {
            for (int i = 0; i < _networkMarkers.Length; i++)
            {
                if (ReflectionUtil.FindTypeByFullName(_networkMarkers[i]) != null)
                    return NetworkPlayerCount.Unknown();
            }

            return NetworkPlayerCount.From(1, "no-network");
        }

        /// <summary>
        /// Photon PUN2. La sonda mas fiable de todas: PhotonNetwork es estatico y CurrentRoom
        /// lo ve CUALQUIER cliente, no solo el servidor.
        ///
        /// Fuera de sala devolvemos 1 y Known: estar en una app de Photon sin sala es estar
        /// solo ahora mismo, y eso si es una medida. Si luego entra en sala, el siguiente
        /// snapshot dira mas y la sesion se cuenta por su maximo.
        /// </summary>
        private static NetworkPlayerCount ProbePhotonPun()
        {
            Type photon = ReflectionUtil.FindTypeByFullName("Photon.Pun.PhotonNetwork");
            if (photon == null)
                return NetworkPlayerCount.Unknown();

            PropertyInfo inRoomProp = photon.GetProperty("InRoom", BindingFlags.Public | BindingFlags.Static);
            if (inRoomProp == null)
                return NetworkPlayerCount.Unknown();

            bool inRoom = Convert.ToBoolean(inRoomProp.GetValue(null));
            if (!inRoom)
                return NetworkPlayerCount.From(1, "photon-pun");

            PropertyInfo roomProp = photon.GetProperty("CurrentRoom", BindingFlags.Public | BindingFlags.Static);
            object room = roomProp == null ? null : roomProp.GetValue(null);
            if (room == null)
                return NetworkPlayerCount.From(1, "photon-pun");

            PropertyInfo countProp = room.GetType().GetProperty("PlayerCount");
            if (countProp == null)
                return NetworkPlayerCount.Unknown();

            return NetworkPlayerCount.From(Convert.ToInt32(countProp.GetValue(room)), "photon-pun");
        }

        /// <summary>
        /// Netcode for GameObjects. ConnectedClientsIds solo esta poblado en el servidor: en
        /// un cliente puro lanza o viene vacio, asi que ahi devolvemos Unknown a proposito.
        ///
        /// En host (servidor + cliente en el mismo proceso) IsServer es true y la lista
        /// incluye al propio host, que es lo que queremos.
        /// </summary>
        private static NetworkPlayerCount ProbeNetcodeForGameObjects()
        {
            Type nmType = ReflectionUtil.FindTypeByFullName("Unity.Netcode.NetworkManager");
            if (nmType == null)
                return NetworkPlayerCount.Unknown();

            PropertyInfo singletonProp = nmType.GetProperty("Singleton", BindingFlags.Public | BindingFlags.Static);
            object manager = singletonProp == null ? null : singletonProp.GetValue(null);
            if (manager == null)
                return NetworkPlayerCount.Unknown();

            PropertyInfo isServerProp = nmType.GetProperty("IsServer");
            if (isServerProp == null || !Convert.ToBoolean(isServerProp.GetValue(manager)))
                return NetworkPlayerCount.Unknown();

            PropertyInfo idsProp = nmType.GetProperty("ConnectedClientsIds");
            ICollection ids = idsProp == null ? null : idsProp.GetValue(manager) as ICollection;
            if (ids == null)
                return NetworkPlayerCount.Unknown();

            return NetworkPlayerCount.From(ids.Count, "netcode-gameobjects");
        }
    }
}

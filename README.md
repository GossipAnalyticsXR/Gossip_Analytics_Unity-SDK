***/\*-> Gossip SDK <-\*/***

## Dependencies

Before using this SDK, make sure the following packages are installed in your Unity project:

### Via Package Manager (Git URL)

- **R3 (Cysharp):** `https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity`

### Via NuGet for Unity

First install NuGetForUnity: `https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity`

Then install via **NuGet → Manage NuGet Packages**:

- R3 by Cysharp
- LiteDB by Mauricio David

### Via Asset Store

- Meta XR Core SDK (free)

> ⚠️ After installing all dependencies, Unity may show warnings about missing .meta files in immutable folders — these are harmless and can be ignored.


**\*-> Dependencias Principales (Package Manager Unity) <-\***

	> UniTask : https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10

	> SocketIOUnity : https://github.com/itisnajim/SocketIOUnity.git#v1.1.4

	> Input System - en "Project Settings > Player > Other Settings", recuerda tener la opción seleccionada "Both" en la variable "Active Input Handling"
	
	> Meta XR Core SDK

	> Meta MR Utility Kit

	> Oculus XR Plugin

	> XR Core Utilities

	> XR Legacy Input Helpers

	> XR Plugin Management





**\*-> Inicialización <-\***

1\. Importar GossipSDK en "Assets", este creará la siguiente carpeta:

	> Assets / GossipSDK

2\. Instala las dependencias que se encuentran arriba, si no las encuentras por nombre en unity "packagemanager", utiliza los links de url para agregarlos, después de instalarlos reinicia Unity.

3\. Crear en tu carpeta "Resources" settings de Gossip "Create > Gossip > Settings".

4\. Ingresar las "URL" y "ApiKeys" que Gossip le proporcione en sus respectivos campos:

	> Dev

	> Beta

	> Production

5\. Selecciona el "Envrioment" que quieras ocupar, al cuál llegaran los datos.

6\. No modifiques "Ingest Path". 

7\. Para montar en escena, ya contamos con un prefab llamado "GossipManager" en la carpeta "Samples" de nuestro SDK, este lleva los componentes primordiales del Manager para que pueda funcionar



**\*-> Trackers <-\***



-> User Trackers <-

	> User Info <

Este tracker se invocará automáticamente cuando la sesión se inicie, este registra lo siguientes datos: lenguaje del dispositivo, edad del usuario, nombre del usuario, código de ciudad, marca del dispositivo, modelo del dispositivo, nombre de OS, versión de OS, estatus de la batería del dispositivo y lenguaje.


	> User Posture <

Este se encarga de registrar la postura del jugador, este tracker se asigna en la cabeza del jugador, el tracker registra la siguiente información: estado de postura, posición de cabeza en los ejes "X", "Y" y "Z" y nombre de escena.

Este tracker se llama con el componente "UserPostureComponent", en este puede modificar lo posición mínima en la registrará el cambio a estado sentado o agachado, con las variables "Sit Threshold", "Crouch Threshold", al igual que podrás asignar el Transform de la cabeza en la variable "Head Transform".


	> User Events <

Este se encarga de registrar eventos ya sean de UI o eventos importantes que se pueden llamar y describir desde Script.

Este tracker se llama con la siguiente línea de código:

	Gossip.Instance.UserEventTracker?.CaptureEvent(string "nombre del evento", string "categoría", string "texto", Vector 3 "posición", Directory<string, object> "propiedades");

Tanto la posición como las propiedades son opcionales.


	> User Balance <

Este se encarga de registar el balance del jugador, este tracker se asigna en la cabeza del jugador, el tracker registra la siguiente información: posición en ejes "X", "Y" y "Z", magnitud de osiclación, frecuencia de osilación y estado de postura.

Este tracker se llama con el componente "UserBalanceTrackerComponent", deberá asignarse en el jugador.

--------------------------------------



-> GameplayMetrics Tracker <-

	> Accessories <

Este se encarga de registrar los accesorios que se vendan, modifiquen o compren dentro del juego, el tracker registra la siguiente información: nombre del producto, precio, marca y compra total.

Este tracker se llama con el componente "AccessoriesComponent", puedes registrar el accesorio con el void ReportPurchased("nombre", "precio", "marca", "compra total").


	> Ads <

Este se encarga de registrar cualquier movimiento de anuncios dentro del juego, desde cuánto dura el anuncio, cuando termina, si da algún tipo recompensa.

Este tracker se llama con el componente "AdComponent", en este se encuentran los siguientes void "StartAd" para registrar el inicio del anuncio, "EndAd" para registrar el fin del anuncio, "RecordImpression" para registrar las veces que se ha reproducido el anuncio, "RecordInteraction" cuántas veces se ha interactuado con este anuncio, "RecordReward" para registar si se ha otorgado alguna recompensa, se pueden modificar las variables de este componente "adId", "adNetwork" y "placementId".


	> Audio Reaction <

Este se encarga de registrar "snippets" cuando detecte que el jugador ha tenido una reacción grotesca o algún grito por parte del usuario, este tracker registra: audio, severidad del evento, cambio de voz, calidad de voz, intensidad de movimiento, puntuación de emoción y modo del trigger.

Este se llama con el componente "AudioReactionTrackerComponent", automáticamente detectará la voz del jugador, solo asegúrate de asignar que la aplicación requiere el micrófono en los configuraciones del proyecto.


	> Audio Volume <

Este se encarga de registar el volumen actual del juego o cuando el volumen cambia, este tracker se llama con el componente "AudioVolumeTrackerComponent", se deberán asignar un Mixer en la variable "audioMixer", en las variables "masterParam", "musicParam" y "sfxParam" se asignarán los nombres de los parámetros a detectar en el Mixer.


	> Avatar <

Este se encarga de registar los avatares que se encuentren, modifiquen, añadan o hagan en el juego, este registra las variables: id del avatar, nombre del avatar, variante, marca, precio y color.

Este tracker se llama con el componente "AvatarTrackerComponent", con el void "NotifyAvatar" podrás registrar la acción del avatar.


	> Battery Monitor <

Este se encarga de registar la batería del dispositivo, el tracker registra los siguientes datos: nivel de batería y estatus de la batería. Este tracker se llama con el componente "BatteryMonitorComponent".


	> Connectivity <

Este se encarga de registrar la velocidad de conexión con la que cuenta el dispositivo, el tracker registra: el tipo de conexión, si está en línea, cantidad de megas de descarga, accesibilidad y nombre de la escena. Este tracker se llama con el componente "ConnectivityMonitorComponent", dentro de este componente se puede modificar la url con la cuál se está detectando la velocidad de la conexión del dispositivo.

	> Difficulty <

Este se encarga de registrar la dificultad con la que cuenta el juego, el tracker registra los siguientes datos: nombre de la escena, dificultad, dificultad numérica, la razón (opcional). Este tracker se llama con el componente "DifficultyComponent".


	> Distance <

Este se encarga de registrar la distancia que hay entre un objeto en específico del jugador, el tracker registra: posición de objeto en "x,y,z" y posición del jugador en "x,y,z", al igual que el nombre de la escena.

Este tracker se llama con el componente "DistanceTrackerComponent" se deberá asignar el player en la variable "playerTransform" para que el update de este funcione.


	> Experience Info <

Este se encarga de registrar la información sobre la experiencia que se está llevando acabo, el tracker registra: tiempo de carga de la experiencia, versión de la aplicación y hardware en el que se encuentra.

Este tracker se llama con el componente "ExperienceInfoComponent", este podrá lanzar la información automatica mente al iniciar si se tiene la variable "autoReportOnStart" active, si no se puede llamar manualmente con la función "SendLoadInfo".


	> Eye Tracking <

Este se encarga de registrar con que objetos esta chocando la mirada del jugador, el tracker registra: nombre del objeto con el que choca, tag del objeto con el choca, posición del choque en "X,Y,Z", duración del choque, nombre de escena y fuente de seguimiento.

Este tracker se llama con el componente "EyeTrackingComponent", este se deberá asignar en la vista del jugador, las variables que se pueden modificar para ajuste de choque son: la distancia máxima del hit y el umbral de fijación. Además este componente también registra en Heatmap, podrás ajustar los valores del Heatmap al que se registra el EyeTracking.

Este tracker contiene una función especial, este tracker detecta si el dispositivo cuenta con eye real / eye simulate. Para poder hacer funcionar recuerda tener la sigueinte configuración:

	> En tu objeto en Escena: OVR Manager > General > Eye Tracking Support > Seleccionar "Requiered"
	> Pestaña Edit > Project Settings > XR-Plug-in Management > Habilitar Bool "Oculus"
	> Pestaña Edit > Project Settings > XR-Plug-in Management > Oculus > Foveated Rendering Method > Seleccionar "Eye Tracked Fovated Rendering"



	> Heatmap <

Este se encarga de crear los Heatmaps de ciertos Trackers que ya están preparados para almacenar esta información, como vendría siendo posición, eye tracking e interacciones. Este tracker no se deberá colocar en escena ya que los componentes con Heatmap disponible lo hará.


	> Hand Controller <

Este se encarga de registrar el movimiento y angulo de las manos durante la experiencia del jugador. Este tracker se llama con el componente "HandControllerTrackingComponent", este automáticamente detectará el uso de cada mano, se debe colocar en cualquier objeto Global, no se sugiere colocar uno por mano ya que provacaría la clonación de datos.


	> Input Usage <

Este se encarga de registrar el tiempo de uso de control o mano que ha hecho el jugador durante toda la experiencia. Este tracker se llama con el componente "InputUsageTrackerComponent", este automáticamente registrará el tiempo de cada uno, al terminar la experiencia enviará el reporte de tiempo de cada uno.


	> Interaction <

Este se encarga de registrar la interacción que ha tenido el jugador con objetos, ya sea que sean interacciones con tiempo ("OnInteractStart" / "OnInteractEnd") o interacciones instantáneas ("OnInteractInstant"), el tracker registra: nombre del objeto, tag del objeto, tipo de interacción, posición y escena.

Este tracker se llama con el componente "InteractableComponent". Además este componente también registra en Heatmap, podrás ajustar los valores del Heatmap al que se registra la interacción.


	> Mistake <

Este se encarga de registrar errores que ocurran al jugar, no reporta automáticamente los errores, deberás llamarlo para los errores que quieras que se reporten, el tracker registra: nombre de objeto, tag de objeto, el error ocurrido, severidad del error, posición y escena. Este tracker se llama con el componente "MistakeReporter", con la función "ReportMistake".


	> Multiplayer <

Este se encarga de registrar datos de usuarios en juegos multijugador, el tracker registra: jugadores que entran y salen de la sala y cuantos jugadores hay activos en la sala. 

Este tracker se llama con el componente "MultiplayerTrackerComponent", dentro vienen las funciones "OnPlayerJoined" / "OnPlayerLeft" para registrar entradas y salidas de usuario y "StartTracking" / "StopTracking" para iniciar o detner captura de datos de la sesión.


	> Memory <

Este se encarga de registrar la cantidad de memoria que está ocupando el programa, el tracker registra: total de bytes asignados, total de bytes reservados, únicos bytes de uso, "GcCollectionsGen0", "GcCollectionsGen1", "GcCollectionsGen2" y cantidad de fps.

Este tracker se llama con el componente "PerformanceMonitorComponent", aquí podrás modificar cada cuánto tiempo se registra la información.


	> Passthrough <

Este se encarga de registrar el passthrough del programa si que está activo, el tracker registra: si está activo, el modo de Passthrough, la exposición y la métrica de calidad. Este tracker se llama con el componente "PassthroughComponent", si la variable activa es verdadera se detectará automáticamente.


	> Pause <

Este se encarga de registrar el momento en que el jugador coloque pausa en el juego, el tracker registra: si el juego está en pausa o se reanudo y cuanto tiempo duró. Este tracker se llama con el componente "PauseComponent" con las funciones "OnPause" o "OnResume".


	> Peripherals <

Este se encarga de registrar que periféricos se están utilizando durante la experiencia, el tracker registra: nombre del periférico, marca, tipo de periférico, si es háptico, tiempo de uso y nombre de escena. Este tracker se llama con el componente "PeripheralAutoTrackerComponent", para este tracker se requiere tener instalada la paquetería "InputSystem".


	> Position <

Este se encarga de registrar la posición actual del jugador, el tracker registra: posición en "X,Y,Z" y el nombre de la escena. Este trakcer se llama con el componente "PositionTrackerComponent", este debe de colocarse en el jugador.


	> Player Movement Heatmap <

Este se encarga de crear el heatmap de la posición del jugador. Este tracker se llama con el componente "PlayerMovementHeatmapComponent", podrás modificar las variables del heatmap dentro de este mismo componente.


	> Reality Mode <

Este se encarga de registrar en que modo de realidad se está utilizando la aplicación, al igual cuántas veces cambia de realidad, el tracker registra: en que modo está, a qué modo cambia, duración del modo previo y nombre de la escena. El tracker se llama con el componente "RealityModeMonitor", el componente detectará automáticamente cuando cambie de realidad.


	> Rotation and Velocity <

Este se encarga de registrar la rotación y velocidad actual del jugador, el tracker registra: rotación en los ejes "X,Y,Z", velocidad, velocidad angular, tiempo y nombre del objeto. Este tracker se llama con el componente "RotationAndVelocityTrackerComponent" y se deberá de colocar en el jugador u objeto del que se necesiten registrar estos datos.


	> Server Status <

Este se encarga de mostrar a nosotros como desarrolladores el estado del servidor de Gossip, el tracker registra: nombre del servidor, estatus, ping ms, porcentaje de carga y meta. Este tracker se llama con el componente "ServerStatusComponent".


	> Session <

Este se encarga de registrar cuando la sesión inicia y termina dentro del servidor, con esto también se detecta cuando el jugador empieza y termina la experiencia. Este tracker registra: nombre del evento, tiempo, duración del evento, id del jugador, id de la sesión. Este tracker se llama con el componente "SessionManager", este se encargará de crear automáticamente el registra cuando la sesión inicie, termine o esté en pausa.






**\*-> Otros Componentes <-\***


	> VR Permissions Handler <
Este script cumple con la función de solicitar permisos como: Datos espaciales, cámara, micrófono, y eyeHead. Para que el SDK pueda cumplir su función, si tu ya cuentas con un script que cumpla con esta función no es necesario que lo coloques en escena,

	> XR Bootstrap <
Ya que nuestro SDK es OpenXR-first, ocupamos este script para poder hacer el llamado de los datos necesarios de "OpenXR", recuerda colocar este script en escena para que nuestro SDK pueda cumplir su función y evitar errores.





**\*-> Image Heatmap <-\***


	> Heatmap Ortographic Image <

Este se disparará una vez por versión caundo el SDK se encuentre en modo "Production", lo que hará será tomar un cálculo del tamaño de la escena y enviará la información de la imagen a sevidor. Solo se enviará la información si se encuentram en modo "Production".

	> Interaction Image <

Este se disparará automaticamente, este se enviará gracias al componente de "Interaction". Solo se enviará la información si se encuentram en modo "Production".

	> Eye Gaze Image <

Este se disparará automaticamente en el componente "EyeTrackingComponent", solo se enviará la información si se encuentra en el modo "Production".

Gossip Analytics
Contacto de soporte: "support@gossipanalytics.com"





**\*-> Notas importantes <-\***

		> Este SDK está diseñado para ser OpenXR-first, por lo tanto es necesario colocar en escena el componente "XR Bootstrap"
		> Para que los heatmaps se habiliten, recuerda marcar "enableHeatmaps" en los settings.
		> Todos los tracker que se llamen con "Componentes" deberán colocarse en escena para que estos funcionen.
		> Verifica que ingresaste bein tus "apiKey" y "Url" para que funcione el SDK.
		> Mantente al tanto de pagar tu servicio con Gossip para que el SDK funcione.
		> Recuerda que la imágenes solo se enviarán en el "Envrioment" "Production".
		> Este SDK requiere permisos de uso de "micrófono" y "cámara".
		> No cambiar el nombre de "GossipAnalyticsSettings" en el asset de "Settings".


> .Gossip Analytics SDK. <

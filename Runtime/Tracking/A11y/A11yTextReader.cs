using System;
using System.Collections.Generic;
using System.Reflection;

namespace GossipSDK.Tracking.A11y
{
    /// <summary>
    /// Lo que la pasada geometrica consigue leer de un componente de texto, y lo
    /// que no.
    ///
    /// Cada dato viene con su bandera. No hay valores por defecto que parezcan
    /// medidas: un tamano de fuente que no se pudo leer sale con su Has... en
    /// false, y la metrica que lo necesitaba saca ese elemento de SU denominador
    /// y lo cuenta. Un 0 en su lugar diria que el texto mide cero, que es otra
    /// cosa -- es el patron del cero que finge ser un dato, pero en el origen.
    /// </summary>
    public struct TextReading
    {
        public bool HasFontSize;
        public float FontSize;

        public bool HasBold;
        public bool Bold;

        public bool HasText;
        public string Text;

        public bool HasPreferredSize;
        public float PreferredWidth;
        public float PreferredHeight;

        public bool HasLineCount;
        public int LineCount;
    }

    /// <summary>
    /// Lee tamano de fuente, negrita, texto, tamano preferido y numero de lineas
    /// de un componente de texto SIN referenciar TextMeshPro.
    ///
    /// Por que por reflexion y no con una referencia de asmdef: este paquete no
    /// depende de TMP y no puede empezar a depender. Medido el 3-oct-2026 sobre
    /// los 162 ficheros .cs del repo, 0 fallos: "using TMPro" aparece en cuatro,
    /// y los cuatro viven bajo Samples~, que Unity no importa. package.json no
    /// declara dependencies, el asmdef del Runtime no referencia TMP y el
    /// comprobador de dependencias no lo nombra. Y una referencia de asmdef a un
    /// paquete ausente no es un aviso: rompe la compilacion del ensamblado
    /// entero en el proyecto del cliente, y este SDK se instala en proyectos
    /// arbitrarios.
    ///
    /// El precio es que no hay seguridad de tipos. Se paga asi: cada lectura va
    /// dentro de su try, y lo que falla no se inventa -- se marca como no leido.
    /// La pasada corre una vez por carga de escena y el plan de lectura se
    /// resuelve una sola vez por tipo, asi que el coste no se nota.
    /// </summary>
    public static class A11yTextReader
    {
        /// <summary>
        /// Los dos tipos base que cuentan como texto. Se compara el nombre
        /// COMPLETO y se recorre la cadena de herencia, asi que una subclase del
        /// cliente entra, y una clase suya llamada "Text" en otro espacio de
        /// nombres no.
        ///
        /// TMP_Text es la base de TextMeshProUGUI y tambien de TextMeshPro -- el
        /// de mundo --, y las dos derivan de MaskableGraphic, asi que las dos
        /// salen en un barrido de Graphic.
        /// </summary>
        public static readonly string[] TextBaseTypes =
        {
            "UnityEngine.UI.Text",
            "TMPro.TMP_Text"
        };

        /// <summary>
        /// Una propiedad O un campo. Hacen falta los dos: TMP_Text expone casi
        /// todo como propiedad, pero TMP_TextInfo declara lineCount como CAMPO
        /// publico. Buscando solo propiedades, el numero de filas no se leia
        /// nunca en TextMeshPro, que es la implementacion que usa casi todo el
        /// mundo, y las metricas que dependen de el salian con denominador
        /// cero sin que nada avisara.
        /// </summary>
        private sealed class Miembro
        {
            public PropertyInfo Propiedad;
            public FieldInfo Campo;

            public Type Tipo
            {
                get { return Propiedad != null ? Propiedad.PropertyType : Campo.FieldType; }
            }

            public object Leer(object objetivo)
            {
                return Propiedad != null
                    ? Propiedad.GetValue(objetivo, null)
                    : Campo.GetValue(objetivo);
            }
        }

        private sealed class Plan
        {
            public Miembro FontSize;
            public Miembro FontStyle;
            public Miembro Text;
            public Miembro PreferredWidth;
            public Miembro PreferredHeight;
            public Miembro LineHolder;
            public Miembro LineCount;
        }

        private static readonly Dictionary<Type, Plan> Plans = new Dictionary<Type, Plan>();

        /// <summary>Vacia la cache de planes. Solo las pruebas la necesitan.</summary>
        public static void ClearCache()
        {
            lock (Plans)
            {
                Plans.Clear();
            }
        }

        public static bool IsTextComponent(object component)
        {
            if (component == null) return false;
            return DerivesFromAny(component.GetType(), TextBaseTypes);
        }

        /// <summary>
        /// Sube por la cadena de herencia comparando el nombre COMPLETO del tipo.
        /// Publico porque es lo unico de la puerta que se puede probar sin Unity:
        /// el ensamblado de pruebas no referencia UnityEngine.UI, asi que no hay
        /// forma de instanciar un Text de verdad ahi dentro.
        /// </summary>
        public static bool DerivesFromAny(Type type, string[] fullNames)
        {
            if (type == null || fullNames == null) return false;

            for (var t = type; t != null; t = t.BaseType)
            {
                var name = t.FullName;
                for (int i = 0; i < fullNames.Length; i++)
                {
                    if (fullNames[i] == name) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Lo que llama la pasada: comprueba el tipo y lee. Devuelve false cuando
        /// el componente no es texto, que no es lo mismo que no haber podido leer
        /// sus miembros.
        /// </summary>
        public static bool TryRead(object component, out TextReading reading)
        {
            reading = new TextReading();
            if (!IsTextComponent(component)) return false;
            ReadMembers(component, ref reading);
            return true;
        }

        /// <summary>
        /// Lee lo que encuentre, sin preguntar de que tipo es. Existe aparte
        /// porque una prueba no puede fabricar un doble llamado TMPro.TMP_Text
        /// sin chocar con el de verdad cuando el paquete esta instalado.
        /// </summary>
        public static void ReadMembers(object component, ref TextReading reading)
        {
            if (component == null) return;

            var plan = PlanFor(component.GetType());

            float size;
            if (TryNumber(plan.FontSize, component, out size))
            {
                reading.HasFontSize = true;
                reading.FontSize = size;
            }

            bool bold;
            if (TryBold(plan.FontStyle, component, out bold))
            {
                reading.HasBold = true;
                reading.Bold = bold;
            }

            object text;
            if (TryValue(plan.Text, component, out text) && text is string)
            {
                reading.HasText = true;
                reading.Text = (string)text;
            }

            // Ancho y alto van juntos: media caja no es una caja, y R5 compara
            // las dos dimensiones contra el rect del contenedor.
            float width;
            float height;
            if (TryNumber(plan.PreferredWidth, component, out width)
                && TryNumber(plan.PreferredHeight, component, out height))
            {
                reading.HasPreferredSize = true;
                reading.PreferredWidth = width;
                reading.PreferredHeight = height;
            }

            int lines;
            if (TryLineCount(plan, component, out lines))
            {
                reading.HasLineCount = true;
                reading.LineCount = lines;
            }
        }

        private static Plan PlanFor(Type type)
        {
            Plan plan;
            lock (Plans)
            {
                if (Plans.TryGetValue(type, out plan)) return plan;
            }

            plan = new Plan
            {
                FontSize = Find(type, "fontSize"),
                FontStyle = Find(type, "fontStyle"),
                Text = Find(type, "text"),
                PreferredWidth = Find(type, "preferredWidth"),
                PreferredHeight = Find(type, "preferredHeight")
            };

            // El mismo dato por dos caminos: TMP lo deja en textInfo.lineCount y
            // el Text de uGUI en cachedTextGenerator.lineCount.
            plan.LineHolder = Find(type, "textInfo");
            if (plan.LineHolder == null) plan.LineHolder = Find(type, "cachedTextGenerator");
            if (plan.LineHolder != null)
            {
                plan.LineCount = Find(plan.LineHolder.Tipo, "lineCount");
            }

            lock (Plans)
            {
                Plans[type] = plan;
            }

            return plan;
        }

        /// <summary>
        /// Busca el miembro bajando por la cadena de herencia con DeclaredOnly
        /// y se queda con el mas derivado. Con FlattenHierarchy, una propiedad
        /// redeclarada con "new" lanza AmbiguousMatchException y la lectura se
        /// perderia entera sin que nadie supiera por que.
        ///
        /// Mira primero la propiedad y despues el campo, en cada nivel. Un tipo
        /// que declare las dos cosas con el mismo nombre no existe en C#, asi
        /// que el orden solo decide cuando conviven en niveles distintos, y ahi
        /// gana el mas derivado, que es lo mismo que haria el compilador.
        /// </summary>
        private static Miembro Find(Type type, string name)
        {
            const BindingFlags donde =
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

            for (var t = type; t != null; t = t.BaseType)
            {
                var prop = t.GetProperty(name, donde);
                if (prop != null && prop.CanRead && prop.GetIndexParameters().Length == 0)
                {
                    return new Miembro { Propiedad = prop };
                }

                var campo = t.GetField(name, donde);
                if (campo != null)
                {
                    return new Miembro { Campo = campo };
                }
            }

            return null;
        }

        private static bool TryValue(Miembro prop, object target, out object value)
        {
            value = null;
            if (prop == null || target == null) return false;

            try
            {
                value = prop.Leer(target);
            }
            catch (Exception)
            {
                // TMP recalcula el layout al leer preferredWidth y puede lanzar
                // sobre un objeto a medio montar. Eso es "no se pudo leer".
                return false;
            }

            return true;
        }

        private static bool TryNumber(Miembro prop, object target, out float number)
        {
            number = 0f;

            object raw;
            if (!TryValue(prop, target, out raw)) return false;
            if (raw == null) return false;

            if (!(raw is float || raw is double || raw is int
                  || raw is long || raw is short || raw is decimal))
            {
                return false;
            }

            try
            {
                number = Convert.ToSingle(raw);
            }
            catch (Exception)
            {
                return false;
            }

            // Un NaN o un infinito no es una medida.
            if (float.IsNaN(number) || float.IsInfinity(number)) return false;

            return true;
        }

        private static bool TryBold(Miembro prop, object target, out bool bold)
        {
            bold = false;

            object raw;
            if (!TryValue(prop, target, out raw)) return false;
            if (raw == null || !raw.GetType().IsEnum) return false;

            // Se lee el NOMBRE, no el numero. UnityEngine.FontStyle y
            // TMPro.FontStyles coinciden hoy en que Bold vale 1, pero son dos
            // enumeraciones distintas -- una de ellas de banderas -- y esa
            // coincidencia no es un contrato de nadie. ToString() de una
            // enumeracion de banderas da "Bold, Italic".
            var texto = raw.ToString();
            if (string.IsNullOrEmpty(texto)) return false;

            var partes = texto.Split(',');
            for (int i = 0; i < partes.Length; i++)
            {
                var parte = partes[i].Trim();
                if (parte == "Bold" || parte == "BoldAndItalic")
                {
                    bold = true;
                    break;
                }
            }

            return true;
        }

        private static bool TryLineCount(Plan plan, object target, out int lines)
        {
            lines = 0;
            if (plan.LineHolder == null || plan.LineCount == null) return false;

            object holder;
            if (!TryValue(plan.LineHolder, target, out holder) || holder == null) return false;

            object raw;
            if (!TryValue(plan.LineCount, holder, out raw)) return false;
            if (!(raw is int)) return false;

            lines = (int)raw;
            if (lines < 0) return false;

            return true;
        }
    }
}

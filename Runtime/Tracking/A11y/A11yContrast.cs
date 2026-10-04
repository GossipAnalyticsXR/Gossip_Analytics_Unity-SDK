using System;

namespace GossipSDK.Tracking.A11y
{
    /// <summary>
    /// El contraste de WCAG, y solo eso: aritmetica sobre tres canales. No
    /// toca Unity a proposito, para que las pruebas de editor puedan ejecutar
    /// la formula entera sin montar una escena.
    ///
    /// LOS CANALES SE LEEN COMO sRGB. Un UnityEngine.Color guarda lo que se
    /// escribio en el Inspector, que es sRGB aunque el proyecto renderice en
    /// espacio lineal: la conversion la hace el shader, no el campo. Si algun
    /// dia se le pasaran valores ya linealizados, la formula de abajo los
    /// linealizaria dos veces y el ratio saldria mas alto de lo que es.
    /// </summary>
    public static class A11yContrast
    {
        /// <summary>WCAG 2.2, 1.4.3 (AA). Texto normal.</summary>
        public const float RatioMinimoDeTexto = 4.5f;

        /// <summary>WCAG 2.2, 1.4.3 (AA). Texto grande.</summary>
        public const float RatioMinimoDeTextoGrande = 3f;

        /// <summary>WCAG 2.2, 1.4.11 (AA). Elementos no textuales.</summary>
        public const float RatioMinimoDeNoTexto = 3f;

        /// <summary>
        /// Por debajo de esta alfa una superficie no es un fondo: es un filtro
        /// sobre lo que haya detras, y su color no es el color que se ve. El
        /// 0,95 no sale de ninguna norma, es el corte que esta pasada declara
        /// para no inventarse el resultado de una mezcla.
        /// </summary>
        public const float AlfaOpaca = 0.95f;

        /// <summary>
        /// WCAG define texto grande como 18 pt, o 14 pt en negrita. El numero
        /// que se compara aqui es el fontSize del componente, que Unity
        /// documenta en puntos para uGUI y que TMP usa en la misma escala
        /// nominal. ES UNA EQUIVALENCIA ASUMIDA: un canvas escalado dibuja ese
        /// mismo numero mas grande o mas pequeno. Quien mide el tamano
        /// RENDERIZADO es P4, con la camara delante; aqui el numero solo decide
        /// si se aplica la excepcion, nunca si el texto se lee.
        /// </summary>
        public const float PuntosDeTextoGrande = 18f;

        public const float PuntosDeTextoGrandeEnNegrita = 14f;

        /// <summary>
        /// Luminancia relativa de WCAG 2.x. Los canales entran en 0..1 sRGB.
        /// </summary>
        public static float LuminanciaRelativa(float r, float g, float b)
        {
            return (float)(
                0.2126 * Linealizar(r) +
                0.7152 * Linealizar(g) +
                0.0722 * Linealizar(b)
            );
        }

        private static double Linealizar(float canal)
        {
            double c = canal;
            if (c < 0.0) c = 0.0;
            if (c > 1.0) c = 1.0;
            if (c <= 0.04045) return c / 12.92;
            return Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        /// <summary>
        /// Ratio de contraste entre dos colores. Simetrico: da lo mismo cual se
        /// pase primero, porque la formula pone arriba al mas claro.
        /// </summary>
        public static float Ratio(
            float r1, float g1, float b1,
            float r2, float g2, float b2
        )
        {
            var a = LuminanciaRelativa(r1, g1, b1);
            var b = LuminanciaRelativa(r2, g2, b2);
            var claro = a > b ? a : b;
            var oscuro = a > b ? b : a;
            return (claro + 0.05f) / (oscuro + 0.05f);
        }

        /// <summary>
        /// Si el tamano no se pudo leer, NO hay excepcion: se juzga a 4,5:1.
        /// Lo contrario seria regalar el aprobado a lo que no se pudo medir.
        /// </summary>
        public static bool EsTextoGrande(bool hayTamano, float puntos, bool negrita)
        {
            if (!hayTamano) return false;
            if (puntos >= PuntosDeTextoGrande) return true;
            return negrita && puntos >= PuntosDeTextoGrandeEnNegrita;
        }

        public static bool TextoCumple(float ratio, bool esGrande)
        {
            return ratio >= (esGrande ? RatioMinimoDeTextoGrande : RatioMinimoDeTexto);
        }

        public static bool NoTextoCumple(float ratio)
        {
            return ratio >= RatioMinimoDeNoTexto;
        }

        public static bool EsOpaca(float alfa)
        {
            return alfa >= AlfaOpaca;
        }
    }
}

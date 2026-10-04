using System;

namespace GossipSDK.Tracking.A11y
{
    /// <summary>
    /// La aritmetica de la pasada geometrica, sin una sola referencia a Unity.
    ///
    /// Esta aparte del tracker por dos razones. La primera es que asi se puede
    /// compilar y EJECUTAR fuera del editor, que es la unica forma de ver fallar
    /// un umbral antes de que lo vea un cliente. La segunda es que aqui viven
    /// los numeros publicados, y conviene que esten en un sitio pequeno que se
    /// pueda leer entero.
    ///
    /// Regla que gobierna este fichero: NO SE INVENTA UN UMBRAL. Lo que se
    /// escribe son los numeros tal y como los publican sus documentos; el
    /// milimetro con el que luego se compara se CALCULA de ellos. Teclear "22
    /// mm" seria meter un redondeo mio en el sitio donde deberia estar la
    /// fuente.
    /// </summary>
    public static class A11yGeometry
    {
        /// <summary>
        /// Guia de diseno de Meta para dianas, citada por WCAG 2.2 2.5.8 en el
        /// registro de ACC-41: "48 dp, or 3 degrees of FOV at 0.42 m".
        /// </summary>
        public const float GradosMinimosDeDiana = 3f;
        public const float DistanciaDeReferenciaDeDiana = 0.42f;

        /// <summary>
        /// Google Daydream UX-D1: "1.5 degrees of FOV, at 0.5 m or more". Son
        /// DOS condiciones, no una: un texto enorme a 20 cm sigue sin cumplir,
        /// porque el problema a esa distancia es la vergencia, no el tamano.
        /// </summary>
        public const float GradosMinimosDeTexto = 1.5f;
        public const float DistanciaMinimaDeTexto = 0.5f;

        /// <summary>
        /// Cuanto mide, a una distancia dada, algo que subtiende esos grados.
        /// </summary>
        public static float TamanoQueSubtiende(float grados, float distancia)
        {
            if (distancia <= 0f || grados <= 0f) return 0f;
            if (grados >= 180f) return 0f;
            return (float)(2.0 * distancia * Math.Tan(grados * Math.PI / 360.0));
        }

        /// <summary>
        /// Cuantos grados subtiende algo de ese tamano a esa distancia.
        /// </summary>
        public static float GradosQueSubtiende(float tamano, float distancia)
        {
            if (distancia <= 0f || tamano <= 0f) return 0f;
            return (float)(2.0 * Math.Atan((tamano * 0.5) / distancia) * 180.0 / Math.PI);
        }

        /// <summary>
        /// El lado menor de la cara mayor de una caja, que resulta ser la
        /// MEDIANA de sus tres dimensiones: ordenados de menor a mayor como a,
        /// b, c, la cara mayor es la de b por c y su lado corto es b.
        ///
        /// Por que no el lado menor a secas: un boton es una placa, y su
        /// dimension mas pequena es el grosor. Medir por ahi suspenderia a todo
        /// elemento plano, que es casi todo. Y por que no el mayor: WCAG 2.5.8
        /// pide que la diana contenga un CUADRADO de la medida minima, asi que
        /// manda el lado corto de la cara por la que se apunta.
        /// </summary>
        public static float LadoMenorDeLaCaraMayor(float x, float y, float z)
        {
            var a = Math.Abs(x);
            var b = Math.Abs(y);
            var c = Math.Abs(z);

            // Se ordenan los tres y se devuelve el de en medio. Se hace por
            // comparacion y no restando los extremos a la suma: eso daria el
            // valor correcto en aritmetica exacta y 0,20000002 en coma
            // flotante, y aqui se compara contra un umbral.
            float t;
            if (a > b) { t = a; a = b; b = t; }
            if (b > c) { t = b; b = c; c = t; }
            if (a > b) { t = a; a = b; b = t; }

            return b;
        }

        /// <summary>
        /// O1. El tamano minimo es una propiedad del objeto, no del sitio desde
        /// donde se mire: la cifra de Meta esta dada a una distancia fija, asi
        /// que lo que se compara es un tamano, y el resultado no depende de
        /// hacia donde mirase el usuario cuando cargo la escena.
        /// </summary>
        public static bool DianaCumple(float ladoMenorDeLaCaraMayor)
        {
            if (ladoMenorDeLaCaraMayor <= 0f) return false;
            return ladoMenorDeLaCaraMayor >= TamanoMinimoDeDiana();
        }

        public static float TamanoMinimoDeDiana()
        {
            return TamanoQueSubtiende(GradosMinimosDeDiana, DistanciaDeReferenciaDeDiana);
        }

        /// <summary>
        /// P4. Aqui si manda la camara: el requisito de Daydream habla de lo que
        /// el usuario ve, no de lo que la escena contiene.
        /// </summary>
        public static bool TextoCumple(float altoDeLinea, float distancia)
        {
            if (altoDeLinea <= 0f || distancia <= 0f) return false;
            if (distancia < DistanciaMinimaDeTexto) return false;
            return GradosQueSubtiende(altoDeLinea, distancia) >= GradosMinimosDeTexto;
        }
    }
}

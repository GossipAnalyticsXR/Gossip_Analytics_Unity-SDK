namespace GossipSDK.A11y
{
    /// <summary>
    /// Declara que este bloque de texto es un SUBTITULO. Sin esta marca U1 no
    /// tiene poblacion: el barrido del 2-oct-2026 sobre los 164 ficheros del
    /// SDK encontro cero apariciones de caption o subtitle, asi que no habia
    /// forma de distinguir un subtitulo de un parrafo. Adivinarlo por la forma
    /// del texto aplicaria la regla de 32 caracteres por fila a parrafos que
    /// nunca pretendieron ser subtitulos, y el numero no significaria nada.
    /// </summary>
    public class GossipA11yCaption : UnityEngine.MonoBehaviour
    {
    }
}

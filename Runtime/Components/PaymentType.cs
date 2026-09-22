namespace GossipSDK
{
    public enum PaymentType
    {
        Card,
        Cryptocurrency,
        GiftCard,
        Other,

        // Va EL ULTIMO a proposito. Unity serializa los enum por su valor entero, asi
        // que insertarlo antes cambiaria en silencio lo que ya esta guardado en las
        // escenas del integrador: un Card pasaria a ser Cryptocurrency.
        //
        // Es el valor por defecto de los componentes NUEVOS. Los que ya estan
        // colocados en una escena conservan el suyo: no se reescribe el proyecto de
        // nadie.
        //
        // Cuando el componente resuelve a este valor, NO manda el campo. "No
        // declarado" no es una forma de pago, y mandar una inventada es lo que hacia
        // que el dashboard dijera "Real money" de una compra que nadie declaro: Card
        // era ademas el valor 0 del enum, asi que un componente recien anadido y uno
        // donde alguien eligio Card a mano se guardaban identicos.
        NotDeclared
    }
}

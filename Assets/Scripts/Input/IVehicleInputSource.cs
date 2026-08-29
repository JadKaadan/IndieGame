namespace IndieGame.VehicleInput
{
    /// <summary>
    /// Anything that can drive a vehicle: the local player, an AI traffic
    /// driver, a replay playback head, or a remote network peer.
    /// </summary>
    public interface IVehicleInputSource
    {
        /// <summary>
        /// Returns the accumulated input since the last call and clears any
        /// buffered one-shot commands. Called once per FixedUpdate by
        /// VehicleController.
        /// </summary>
        VehicleInputState ConsumeInput();
    }
}

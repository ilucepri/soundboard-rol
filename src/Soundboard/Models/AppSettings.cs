namespace Soundboard.Models;

/// <summary>Preferencias que sobreviven entre arranques. No incluye los perfiles.</summary>
public sealed class AppSettings
{
    /// <summary>Id del dispositivo que va a Wave Link (lo que oyen los demás). Null = ninguno.</summary>
    public string? BroadcastDeviceId { get; set; }

    /// <summary>Id del dispositivo de escucha propia (tus cascos). Null = ninguno.</summary>
    public string? MonitorDeviceId { get; set; }

    /// <summary>
    /// Nombres de esos mismos dispositivos. Wave Link destruye y recrea sus endpoints —con ids
    /// nuevos— cada vez que añades, quitas o renombras un canal, así que el id solo no basta para
    /// reencontrarlos entre arranques.
    /// </summary>
    public string? BroadcastDeviceName { get; set; }

    public string? MonitorDeviceName { get; set; }

    public double BroadcastVolume { get; set; } = 0.8;

    public double MonitorVolume { get; set; } = 0.5;

    public Guid? LastProfileId { get; set; }
}

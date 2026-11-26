/// <summary>
/// Implementasikan interface ini di script pintu mana pun supaya
/// enemy bisa membukanya.
/// </summary>
public interface IEnemyOpenable
{
    /// <summary>True kalau pintu sedang terbuka (atau sedang membuka).</summary>
    bool IsOpen { get; }

    /// <summary>
    /// Dipanggil enemy untuk membuka pintu.
    /// Jangan memancarkan NoiseSystem di sini, supaya enemy tidak
    /// "mendengar" dirinya sendiri.
    /// </summary>
    void OpenByEnemy();
}
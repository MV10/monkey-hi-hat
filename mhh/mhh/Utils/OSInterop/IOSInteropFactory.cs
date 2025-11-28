
namespace mhh;

public interface IOSInteropFactory<T> where T : IOSInteropFactory<T>, IOSInterop
{
    /// <summary>
    /// Returns an instance of the concrete IOSInterop implementation.
    /// </summary>
    static abstract T Create();

    /// <summary>
    /// Returns an instance of the concrete IOSInterop implementation.
    /// </summary>
    static abstract Task<T> CreateAsync();
}

using System.Collections.Concurrent;

/// <summary>
/// Dependency injection container for managing object instances.
/// </summary>
// ReSharper disable once InconsistentNaming
public class DIContainer
{
    #region Fields

    private static DIContainer _global;
    private readonly ConcurrentDictionary<string, object> _objContainer = new();

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the current scoped container.
    /// </summary>
    public static DIContainer Current { get; set; }

    /// <summary>
    /// Gets the global container that persists throughout the application lifecycle.
    /// </summary>
    public static DIContainer Global
    {
        get
        {
            _global ??= new DIContainer();
            return _global;
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Generates a unique key for container registration.
    /// </summary>
    /// <param name="fieldType">The type of the object.</param>
    /// <param name="key">Optional key for distinguishing multiple registrations of the same type.</param>
    /// <returns>A unique string key.</returns>
    private static string GenKey(Type fieldType, string key = "")
    {
        return fieldType.FullName + "__" + key;
    }

    #endregion

    #region Registration

    /// <summary>
    /// Registers an object in the container.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="obj">The object to register.</param>
    /// <exception cref="InvalidOperationException">Thrown when an object with the same key is already registered.</exception>
    public void Register<T>(T obj)
    {
        var key = GenKey(typeof(T));
        if (!_objContainer.TryAdd(key, obj))
        {
            throw new InvalidOperationException($"An object with key '{key}' is already registered.");
        }
    }

    /// <summary>
    /// Registers an object in the container with a named key.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="obj">The object to register.</param>
    /// <param name="name">The name to distinguish this registration.</param>
    /// <exception cref="InvalidOperationException">Thrown when an object with the same key is already registered.</exception>
    public void Register<T>(T obj, string name)
    {
        var key = GenKey(typeof(T), name);
        if (!_objContainer.TryAdd(key, obj))
        {
            throw new InvalidOperationException($"An object with key '{key}' is already registered.");
        }
    }

    /// <summary>
    /// Updates or registers an object in the container.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="obj">The object to register or update.</param>
    /// <param name="name">Optional name to distinguish this registration.</param>
    public void UpdateRegistration<T>(T obj, string name = "")
    {
        var key = GenKey(typeof(T), name);
        _objContainer[key] = obj;
    }

    /// <summary>
    /// Updates or registers an object in the container by type.
    /// </summary>
    /// <param name="type">The type of the object.</param>
    /// <param name="obj">The object to register or update.</param>
    /// <param name="key">Optional key to distinguish this registration.</param>
    public void UpdateRegistration(Type type, object obj, string key = "")
    {
        key = GenKey(type, key);
        _objContainer[key] = obj;
    }

    /// <summary>
    /// Removes an object from the container.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="name">Optional name of the registration.</param>
    /// <exception cref="KeyNotFoundException">Thrown when no object with the specified key is registered.</exception>
    public void Unregister<T>(string name = "")
    {
        var key = GenKey(typeof(T), name);
        if (!_objContainer.TryRemove(key, out _))
        {
            throw new KeyNotFoundException($"No object with key '{key}' is registered.");
        }
    }

    /// <summary>
    /// Removes an object from the container by type.
    /// </summary>
    /// <param name="type">The type of the object.</param>
    /// <param name="key">Optional key of the registration.</param>
    /// <exception cref="KeyNotFoundException">Thrown when no object with the specified key is registered.</exception>
    public void Unregister(Type type, string key = "")
    {
        key = GenKey(type, key);
        if (!_objContainer.TryRemove(key, out _))
        {
            throw new KeyNotFoundException($"No object with key '{key}' is registered.");
        }
    }

    /// <summary>
    /// Removes all objects from the container.
    /// </summary>
    public void Clear()
    {
        _objContainer.Clear();
    }

    #endregion

    #region Resolution

    /// <summary>
    /// Retrieves an object from the container by type and key.
    /// </summary>
    /// <param name="type">The type of the object.</param>
    /// <param name="key">The key of the registration.</param>
    /// <returns>The registered object, or null if not found.</returns>
    public object Get(Type type, string key)
    {
        key = GenKey(type, key);
        return _objContainer.GetValueOrDefault(key);
    }

    /// <summary>
    /// Retrieves an object from the container by type.
    /// </summary>
    /// <param name="type">The type of the object.</param>
    /// <returns>The registered object, or null if not found.</returns>
    public object Get(Type type)
    {
        var key = GenKey(type);
        return _objContainer.GetValueOrDefault(key);
    }

    /// <summary>
    /// Retrieves a typed object from the container.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="name">Optional name of the registration.</param>
    /// <returns>The registered object, or null if not found.</returns>
    public T Get<T>(string name = "") where T : class
    {
        var key = GenKey(typeof(T), name);
        if (_objContainer.TryGetValue(key, out object value))
        {
            return value as T;
        }

        return null;
    }

    /// <summary>
    /// Checks if an object is registered in the container.
    /// </summary>
    /// <param name="type">The type of the object.</param>
    /// <param name="key">The key of the registration.</param>
    /// <returns>True if the object is registered, false otherwise.</returns>
    public bool Has(Type type, string key)
    {
        var generatedKey = GenKey(type, key);
        return _objContainer.ContainsKey(generatedKey);
    }

    /// <summary>
    /// Resolves a dependency from Current container first, then falls back to Global.
    /// Used by the Source Generator for lazy injection.
    /// </summary>
    /// <param name="fieldType">The type of the dependency.</param>
    /// <param name="key">Optional key for the dependency.</param>
    /// <returns>The resolved object, or null if not found.</returns>
    public static object GetValue(Type fieldType, string key = "")
    {
        object value = null;
        if (Current != null && Current.Has(fieldType, key))
        {
            value = Current.Get(fieldType, key);
        }

        value ??= Global.Get(fieldType, key);
        return value;
    }

    #endregion
}
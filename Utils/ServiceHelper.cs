namespace SmallBazar.Utils
{
    /// <summary>
    /// Helper class to provide access to services from the IServiceProvider in the MauiApp.
    /// Must be initialized with the IServiceProvider from the MauiApp.
    /// This allows retrieving services from anywhere in the app, even without dependency injection.
    /// </summary>
    public static class ServiceHelper
    {
        /// <summary>
        /// Gets the IServiceProvider instance from the MauiApp.
        /// </summary>
        public static IServiceProvider Services { get; private set; }

        /// <summary>
        /// Initializes the ServiceHelper with the IServiceProvider from the MauiApp.
        /// This method should be called once, right after the host has been built.
        /// </summary>
        /// <param name="serviceProvider">The IServiceProvider instance to initialize with.</param>
        public static void Initialize(IServiceProvider serviceProvider) =>
            Services = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider), "ServiceProvider cannot be null.");

        /// <summary>
        /// Retrieves a registered service from the IServiceProvider.
        /// </summary>
        /// <typeparam name="T">The type of the service to retrieve from the DI container.</typeparam>
        /// <returns>The service instance if found; otherwise, null.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the ServiceHelper has not been initialized.</exception>
        public static T GetService<T>()
        {
            if (Services is null)
                throw new InvalidOperationException("ServiceHelper has not been initialized. Call ServiceHelper.Initialize() from the MauiApp.");

            return Services.GetService<T>();
        }
    }
}

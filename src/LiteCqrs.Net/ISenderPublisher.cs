namespace LiteCqrs;

/// <summary>Convenience composition of <see cref="ISender"/> and <see cref="IPublisher"/> for
/// consumers that want one injected type for both dispatching and publishing. Backed by the same
/// underlying dispatcher instance as injecting <see cref="ISender"/>/<see cref="IPublisher"/>
/// separately — registering all three costs nothing extra.</summary>
public interface ISenderPublisher : ISender, IPublisher;

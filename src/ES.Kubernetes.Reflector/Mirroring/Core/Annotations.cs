namespace ES.Kubernetes.Reflector.Mirroring.Core;

public static class Annotations
{
    public const string Prefix = "reflector.v1.k8s.emberstack.com";

    public static class Reflection
    {
        public static string Allowed => $"{Prefix}/reflection-allowed";
        public static string AllowedNamespaces => $"{Prefix}/reflection-allowed-namespaces";
        public static string AllowedNamespacesSelector => $"{Prefix}/reflection-allowed-namespaces-selector";
        public static string AutoEnabled => $"{Prefix}/reflection-auto-enabled";
        public static string AutoNamespaces => $"{Prefix}/reflection-auto-namespaces";
        public static string AutoNamespacesSelector => $"{Prefix}/reflection-auto-namespaces-selector";
        public static string Reflects => $"{Prefix}/reflects";

        public static string LabelFilter => $"{Prefix}/label-filter";
        public static string AnnotationFilter => $"{Prefix}/annotation-filter";

        public static string MetaAutoReflects => $"{Prefix}/auto-reflects";
        public static string MetaReflectedVersion => $"{Prefix}/reflected-version";
        public static string MetaReflectedAt => $"{Prefix}/reflected-at";
    }

    /// <summary>
    ///     Annotation key prefixes that are always excluded from reflection,
    ///     even when the annotation filter matches them.
    /// </summary>
    public static readonly string[] ExcludedAnnotationPrefixes =
    [
        $"{Prefix}/",
        "kubectl.kubernetes.io/",
        "deployment.kubernetes.io/",
        "argocd.argoproj.io/"
    ];
}
#nullable enable
using System;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;
using LocoSwap.Language;

namespace LocoSwap
{
    /// <summary>
    /// Bindable indexer over the generated <see cref="Resources"/> strings. XAML binds through
    /// this instead of using a one-shot <c>{x:Static lang:Resources.*}</c>, so on-screen text
    /// re-evaluates the moment <see cref="Refresh"/> is called after a language change.
    /// </summary>
    public sealed class LocalizationSource : INotifyPropertyChanged
    {
        public static LocalizationSource Instance { get; } = new LocalizationSource();

        private LocalizationSource() { }

        public string this[string key] =>
            Resources.ResourceManager.GetString(key, Resources.Culture) ?? key;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Re-evaluate every <c>{loc:Loc ...}</c> binding in the app.</summary>
        public void Refresh() =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
    }

    /// <summary>
    /// Markup extension for live-updating localized text: <c>Text="{loc:Loc my_key}"</c>.
    /// </summary>
    public sealed class LocExtension : MarkupExtension
    {
        public LocExtension() { }

        public LocExtension(string key) => Key = key;

        [ConstructorArgument("key")]
        public string Key { get; set; } = "";

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new Binding("[" + Key + "]")
            {
                Source = LocalizationSource.Instance,
                Mode = BindingMode.OneWay,
            };
            return binding.ProvideValue(serviceProvider);
        }
    }
}

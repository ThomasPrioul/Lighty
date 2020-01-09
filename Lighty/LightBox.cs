namespace SourceChord.Lighty
{
    using SourceChord.Lighty.Common;
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Animation;
    using System.Windows.Threading;
    
    public class LightBox : ItemsControl
    {
        Action<FrameworkElement>? _closedDelegate;

        public EventHandler? AllDialogClosed;

        public EventHandler? CompleteInitializeLightBox;

        static LightBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(LightBox), new FrameworkPropertyMetadata(typeof(LightBox)));
        }


        protected override DependencyObject GetContainerForItemOverride()
        {
            return new ContentControl();
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return false;
        }

        public static async void Show(UIElement owner, FrameworkElement content)
        {
            var adorner = GetAdorner(owner);
            if (adorner == null) { adorner = await CreateAdornerAsync(owner); }
            adorner?.Root?.AddDialog(content);
        }

        public static async Task ShowAsync(UIElement owner, FrameworkElement content)
        {
            var adorner = GetAdorner(owner);
            if (adorner == null) { adorner = await CreateAdornerAsync(owner); }

            if (adorner?.Root != null)
                await adorner.Root.AddDialogAsync(content);
        }

        public static void ShowDialog(UIElement owner, FrameworkElement content)
        {
            var adorner = GetAdorner(owner) ?? CreateAdornerModal(owner);
            if (adorner == null) throw new NullReferenceException("Adorner is null");

            var frame = new DispatcherFrame();
            if (adorner.Root != null)
            {
                adorner.Root.AllDialogClosed += (s, e) => { frame.Continue = false; };
                adorner.Root?.AddDialog(content);
            }

            Dispatcher.PushFrame(frame);
        }

        protected static LightBoxAdorner? GetAdorner(UIElement element)
        {
            var win = element as Window;
            var target = win?.Content as UIElement ?? element;

            if (target == null) return null;
            var layer = AdornerLayer.GetAdornerLayer(target);
            if (layer == null) return null;

            var current = layer.GetAdorners(target)
                               ?.OfType<LightBoxAdorner>()
                               ?.SingleOrDefault();

            return current;
        }

        static LightBoxAdorner? CreateAdornerCore(UIElement element, LightBox lightbox)
        {
            var win = element as Window;
            var target = win?.Content as UIElement ?? element;

            if (target == null) return null;
            var layer = AdornerLayer.GetAdornerLayer(target);
            if (layer == null) return null;

            var adorner = new LightBoxAdorner(target);
            adorner.SetRoot(lightbox);

            if (win != null)
            {
                var content = (FrameworkElement)win.Content;
                var margin = content.Margin;
                adorner.Margin = new Thickness(-margin.Left, -margin.Top, margin.Right, margin.Bottom);
                adorner.UseAdornedElementSize = false;
            }

            if (target.IsEnabled)
            {
                target.IsEnabled = false;
                lightbox.AllDialogClosed += (s, e) => { target.IsEnabled = true; };
            }

            lightbox.AllDialogClosed += (s, e) => { layer?.Remove(adorner); };
            layer.Add(adorner);
            return adorner;
        }

        protected static LightBoxAdorner? CreateAdorner(UIElement element)
        {
            return CreateAdornerCore(element, new LightBox());
        }

        protected static Task<LightBoxAdorner?> CreateAdornerAsync(UIElement element)
        {
            var tcs = new TaskCompletionSource<LightBoxAdorner?>();

            var lightbox = new LightBox();
            var adorner = CreateAdornerCore(element, lightbox);
            lightbox.Loaded += (s, e) =>
            {
                if (lightbox.IsParallelInitialize || lightbox.InitializeStoryboard == null)
                {
                    tcs.SetResult(adorner);
                }
                else
                {
                    lightbox.CompleteInitializeLightBox += (_s, _e) =>
                    {
                        tcs.SetResult(adorner);
                    };
                }
            };

            return tcs.Task;
        }

        protected static LightBoxAdorner? CreateAdornerModal(UIElement element)
        {
            var lightbox = new LightBox();

            var adorner = CreateAdornerCore(element, lightbox);
            if (!lightbox.IsParallelInitialize)
            {
                var frame = new DispatcherFrame();
                lightbox.CompleteInitializeLightBox += (s, e) =>
                {
                    frame.Continue = false;
                };

                Dispatcher.PushFrame(frame);
            }

            return adorner;
        }

        protected void AddDialog(FrameworkElement dialog)
        {
            var animation = OpenStoryboard;
            dialog.Loaded += (sender, args) =>
            {
                var container = (FrameworkElement)ContainerFromElement(dialog);
                container.Focus();

                container.MouseLeftButtonDown += (s, e) => { e.Handled = true; };

                var transform = new TransformGroup();
                transform.Children.Add(new ScaleTransform());
                transform.Children.Add(new SkewTransform());
                transform.Children.Add(new RotateTransform());
                transform.Children.Add(new TranslateTransform());
                container.RenderTransform = transform;
                container.RenderTransformOrigin = new Point(0.5, 0.5);

                animation?.BeginAsync(container);
            };

            Items.Add(dialog);

            dialog.CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, async (s, e) =>
            {
                await RemoveDialogAsync(dialog);
            }));

            var parent = (FrameworkElement)dialog.Parent;
            parent.CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, async (s, e) =>
            {
                await RemoveDialogAsync((FrameworkElement)e.Parameter);
            }));

            InvalidateVisual();
        }

        protected async Task<bool> AddDialogAsync(FrameworkElement dialog)
        {
            var tcs = new TaskCompletionSource<bool>();

            var closedHandler = new Action<FrameworkElement>((d) => { });
            closedHandler = new Action<FrameworkElement>((d) =>
            {
                if (d == dialog)
                {
                    tcs.SetResult(true);
                    _closedDelegate -= closedHandler;
                }
            });
            _closedDelegate += closedHandler;

            AddDialog(dialog);

            return await tcs.Task;
        }

        protected async Task RemoveDialogAsync(FrameworkElement dialog)
        {
            int index = Items.IndexOf(dialog);
            int count = Items.Count;

            if (IsParallelDispose)
            {
                var _ = DestroyDialogAsync(dialog);
            }
            else
            {
                await DestroyDialogAsync(dialog);
            }

            if (index != -1 && count == 1)
            {
                await DestroyAdornerAsync();
            }

            _closedDelegate?.Invoke(dialog);
        }

        public override async void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (CloseOnClickBackground)
            {
                MouseLeftButtonDown += (s, e) =>
                {
                    foreach (FrameworkElement item in Items)
                        item.Close();
                };
            }
            await InitializeAdornerAsync();
        }

        protected async Task InitializeAdornerAsync()
        {
            var animation = InitializeStoryboard;
            await animation.BeginAsync(this);
            CompleteInitializeLightBox?.Invoke(this, null);
        }

        protected async Task<bool> DestroyAdornerAsync()
        {
            bool ret = await DisposeStoryboard.BeginAsync(this);
            AllDialogClosed?.Invoke(this, null);
            return ret;
        }

        protected async Task<bool> DestroyDialogAsync(FrameworkElement item)
        {
            var container = (FrameworkElement)ContainerFromElement(item);
            await CloseStoryboard.BeginAsync(container);
            Items.Remove(item);
            return true;
        }

        public bool IsParallelInitialize
        {
            get => (bool)GetValue(IsParallelInitializeProperty);
            set => SetValue(IsParallelInitializeProperty, value);
        }

        public static readonly DependencyProperty IsParallelInitializeProperty =
            DependencyProperty.Register("IsParallelInitialize", typeof(bool), typeof(LightBox), new PropertyMetadata(false));

        public bool IsParallelDispose
        {
            get => (bool)GetValue(IsParallelDisposeProperty);
            set => SetValue(IsParallelDisposeProperty, value);
        }

        public static readonly DependencyProperty IsParallelDisposeProperty =
            DependencyProperty.Register("IsParallelDispose", typeof(bool), typeof(LightBox), new PropertyMetadata(false));

        public Storyboard OpenStoryboard
        {
            get => (Storyboard)GetValue(OpenStoryboardProperty);
            set => SetValue(OpenStoryboardProperty, value);
        }

        public static readonly DependencyProperty OpenStoryboardProperty =
            DependencyProperty.Register("OpenStoryboard", typeof(Storyboard), typeof(LightBox), new PropertyMetadata(null));

        public Storyboard CloseStoryboard
        {
            get => (Storyboard)GetValue(CloseStoryboardProperty);
            set => SetValue(CloseStoryboardProperty, value);
        }

        public static readonly DependencyProperty CloseStoryboardProperty =
            DependencyProperty.Register("CloseStoryboard", typeof(Storyboard), typeof(LightBox), new PropertyMetadata(null));

        public Storyboard InitializeStoryboard
        {
            get => (Storyboard)GetValue(InitializeStoryboardProperty);
            set => SetValue(InitializeStoryboardProperty, value);
        }

        public static readonly DependencyProperty InitializeStoryboardProperty =
            DependencyProperty.Register("InitializeStoryboard", typeof(Storyboard), typeof(LightBox), new PropertyMetadata(null));

        public Storyboard DisposeStoryboard
        {
            get => (Storyboard)GetValue(DisposeStoryboardProperty);
            set => SetValue(DisposeStoryboardProperty, value);
        }

        public static readonly DependencyProperty DisposeStoryboardProperty =
            DependencyProperty.Register("DisposeStoryboard", typeof(Storyboard), typeof(LightBox), new PropertyMetadata(null));

        public bool CloseOnClickBackground
        {
            get => (bool)GetValue(CloseOnClickBackgroundProperty);
            set => SetValue(CloseOnClickBackgroundProperty, value);
        }

        public static readonly DependencyProperty CloseOnClickBackgroundProperty =
            DependencyProperty.Register("CloseOnClickBackground", typeof(bool), typeof(LightBox), new PropertyMetadata(true));
    }
}

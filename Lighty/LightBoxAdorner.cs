using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace SourceChord.Lighty
{
    /// <summary>
    /// 既存のUI上にLightBoxを被せて表示するためのAdorner定義
    /// </summary>
    public class LightBoxAdorner : Adorner
    {
        public LightBox? Root { get; private set; }

        public bool UseAdornedElementSize { get; set; }

        static LightBoxAdorner()
        {
        }

        public LightBoxAdorner(UIElement adornedElement) : base(adornedElement)
        {
            UseAdornedElementSize = true;
        }

        public void SetRoot(LightBox root)
        {
            AddVisualChild(root);
            Root = root;
        }

        protected override int VisualChildrenCount => Root == null ? 0 : 1;

        protected override Visual? GetVisualChild(int index)
        {
            if (index != 0) throw new ArgumentOutOfRangeException();
            return Root;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (Root == null) return default;

            if (UseAdornedElementSize)
            {
                var size = AdornedElement.RenderSize;
                Root.Width = size.Width;
                Root.Height = size.Height;
                Root.Measure(size);
            }
            else
            {
                // ルートのグリッドはAdornerを付けている要素と同じサイズになるように調整
                Root.Width = constraint.Width;
                Root.Height = constraint.Height;
                Root.Measure(constraint);
            }

            return Root.DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Root == null) return default;
            Root.Arrange(new Rect(new Point(0, 0), finalSize));
            return new Size(Root.ActualWidth, Root.ActualHeight);
        }
    }
}

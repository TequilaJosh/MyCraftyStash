using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace MyCraftyStash.Controls
{
    public class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
    {
        public static readonly DependencyProperty ItemWidthProperty =
            DependencyProperty.Register("ItemWidth", typeof(double), typeof(VirtualizingWrapPanel),
                new FrameworkPropertyMetadata(220.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty ItemHeightProperty =
            DependencyProperty.Register("ItemHeight", typeof(double), typeof(VirtualizingWrapPanel),
                new FrameworkPropertyMetadata(280.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty BufferRowsProperty =
            DependencyProperty.Register("BufferRows", typeof(int), typeof(VirtualizingWrapPanel),
                new FrameworkPropertyMetadata(3, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double ItemWidth
        {
            get => (double)GetValue(ItemWidthProperty);
            set => SetValue(ItemWidthProperty, value);
        }

        public double ItemHeight
        {
            get => (double)GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }

        public int BufferRows
        {
            get => (int)GetValue(BufferRowsProperty);
            set => SetValue(BufferRowsProperty, value);
        }

        private Size _extent = new Size(0, 0);
        private Size _viewport = new Size(0, 0);
        private Point _offset;
        private bool _canHorizontallyScroll;
        private bool _canVerticallyScroll;
        private ScrollViewer? _scrollOwner;
        private double _lastMeasureWidth;

        private int GetItemsPerRow(double availableWidth)
        {
            if (availableWidth <= 0 || double.IsInfinity(availableWidth))
                return 1;
            return Math.Max(1, (int)(availableWidth / ItemWidth));
        }

        private int GetTotalItems()
        {
            var itemsControl = ItemsControl.GetItemsOwner(this);
            return itemsControl?.Items.Count ?? 0;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var totalItems = GetTotalItems();
            if (totalItems == 0)
            {
                _extent = new Size(0, 0);
                _viewport = availableSize;
                ScrollOwner?.InvalidateScrollInfo();
                return availableSize;
            }

            if (Math.Abs(availableSize.Width - _lastMeasureWidth) > 1)
            {
                _lastMeasureWidth = availableSize.Width;
            }

            var itemsPerRow = GetItemsPerRow(availableSize.Width);
            var totalRows = (int)Math.Ceiling((double)totalItems / itemsPerRow);

            var newExtent = new Size(availableSize.Width, totalRows * ItemHeight);
            var extentChanged = _extent != newExtent;
            var viewportChanged = _viewport != availableSize;

            _extent = newExtent;
            _viewport = availableSize;

            if (_offset.Y > 0 && _offset.Y + _viewport.Height > _extent.Height)
            {
                _offset.Y = Math.Max(0, _extent.Height - _viewport.Height);
            }

            if (extentChanged || viewportChanged)
            {
                ScrollOwner?.InvalidateScrollInfo();
            }

            var firstVisibleRow = Math.Max(0, (int)(_offset.Y / ItemHeight) - BufferRows);
            var lastVisibleRow = Math.Min(totalRows - 1,
                (int)((_offset.Y + availableSize.Height) / ItemHeight) + BufferRows);

            var firstItem = firstVisibleRow * itemsPerRow;
            var lastItem = Math.Min(totalItems - 1, (lastVisibleRow + 1) * itemsPerRow - 1);

            IItemContainerGenerator generator = ItemContainerGenerator;
            var startPos = generator.GeneratorPositionFromIndex(firstItem);
            var childIndex = startPos.Offset == 0 ? startPos.Index : startPos.Index + 1;

            using (generator.StartAt(startPos, GeneratorDirection.Forward, true))
            {
                for (int itemIndex = firstItem; itemIndex <= lastItem; itemIndex++)
                {
                    bool isNewlyRealized;
                    var child = (UIElement)generator.GenerateNext(out isNewlyRealized);

                    if (isNewlyRealized)
                    {
                        if (childIndex >= InternalChildren.Count)
                            AddInternalChild(child);
                        else
                            InsertInternalChild(childIndex, child);

                        generator.PrepareItemContainer(child);
                    }

                    child.Measure(new Size(ItemWidth, ItemHeight));
                    childIndex++;
                }
            }

            CleanUpItems(firstItem, lastItem);

            return availableSize;
        }

        private void CleanUpItems(int firstItem, int lastItem)
        {
            IItemContainerGenerator generator = ItemContainerGenerator;

            for (int i = InternalChildren.Count - 1; i >= 0; i--)
            {
                var pos = new GeneratorPosition(i, 0);
                int itemIndex = generator.IndexFromGeneratorPosition(pos);

                if (itemIndex == -1 || itemIndex < firstItem || itemIndex > lastItem)
                {
                    generator.Remove(pos, 1);
                    RemoveInternalChildRange(i, 1);
                }
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var itemsPerRow = GetItemsPerRow(finalSize.Width);
            IItemContainerGenerator generator = ItemContainerGenerator;

            for (int i = 0; i < InternalChildren.Count; i++)
            {
                var child = InternalChildren[i];
                int itemIndex = generator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));

                if (itemIndex < 0) continue;

                int row = itemIndex / itemsPerRow;
                int col = itemIndex % itemsPerRow;

                double x = col * ItemWidth;
                double y = row * ItemHeight - _offset.Y;

                child.Arrange(new Rect(x, y, ItemWidth, ItemHeight));
            }

            return finalSize;
        }

        protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
        {
            switch (args.Action)
            {
                case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                case System.Collections.Specialized.NotifyCollectionChangedAction.Move:
                    RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                    IItemContainerGenerator gen = ItemContainerGenerator;
                    for (int i = InternalChildren.Count - 1; i >= 0; i--)
                    {
                        gen.Remove(new GeneratorPosition(i, 0), 1);
                        RemoveInternalChildRange(i, 1);
                    }
                    _offset.Y = 0;
                    ScrollOwner?.InvalidateScrollInfo();
                    break;
            }

            InvalidateMeasure();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            if (sizeInfo.WidthChanged)
            {
                InvalidateMeasure();
            }
        }

        #region IScrollInfo

        public bool CanVerticallyScroll
        {
            get => _canVerticallyScroll;
            set => _canVerticallyScroll = value;
        }

        public bool CanHorizontallyScroll
        {
            get => _canHorizontallyScroll;
            set => _canHorizontallyScroll = value;
        }

        public double ExtentWidth => _extent.Width;
        public double ExtentHeight => _extent.Height;
        public double ViewportWidth => _viewport.Width;
        public double ViewportHeight => _viewport.Height;
        public double HorizontalOffset => _offset.X;
        public double VerticalOffset => _offset.Y;

        public ScrollViewer? ScrollOwner
        {
            get => _scrollOwner;
            set => _scrollOwner = value;
        }

        public void SetHorizontalOffset(double offset)
        {
            _offset.X = 0;
            ScrollOwner?.InvalidateScrollInfo();
        }

        public void SetVerticalOffset(double offset)
        {
            var maxOffset = Math.Max(0, ExtentHeight - ViewportHeight);
            offset = Math.Max(0, Math.Min(offset, maxOffset));

            if (Math.Abs(offset - _offset.Y) > 0.5)
            {
                _offset.Y = offset;
                InvalidateMeasure();
                InvalidateArrange();
                ScrollOwner?.InvalidateScrollInfo();
            }
        }

        public void LineUp() => SetVerticalOffset(VerticalOffset - 20);
        public void LineDown() => SetVerticalOffset(VerticalOffset + 20);
        public void PageUp() => SetVerticalOffset(VerticalOffset - ViewportHeight);
        public void PageDown() => SetVerticalOffset(VerticalOffset + ViewportHeight);
        public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - 48);
        public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + 48);
        public void LineLeft() { }
        public void LineRight() { }
        public void PageLeft() { }
        public void PageRight() { }
        public void MouseWheelLeft() { }
        public void MouseWheelRight() { }

        public Rect MakeVisible(Visual visual, Rect rectangle)
        {
            if (visual is UIElement element)
            {
                int index = InternalChildren.IndexOf(element);
                if (index >= 0)
                {
                    IItemContainerGenerator generator = ItemContainerGenerator;
                    int itemIndex = generator.IndexFromGeneratorPosition(new GeneratorPosition(index, 0));
                    if (itemIndex >= 0)
                    {
                        int itemsPerRow = GetItemsPerRow(_viewport.Width);
                        int row = itemIndex / itemsPerRow;
                        double itemTop = row * ItemHeight;
                        double itemBottom = itemTop + ItemHeight;

                        if (itemTop < _offset.Y)
                            SetVerticalOffset(itemTop);
                        else if (itemBottom > _offset.Y + _viewport.Height)
                            SetVerticalOffset(itemBottom - _viewport.Height);
                    }
                }
            }

            return rectangle;
        }

        #endregion
    }
}

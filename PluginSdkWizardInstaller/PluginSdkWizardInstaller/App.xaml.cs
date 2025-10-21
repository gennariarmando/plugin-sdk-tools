using System;
using System.Windows;
using System.Windows.Controls;

namespace PluginSdkWizardInstaller
{
    public class GridSpacingSetter
    {
        // utility for recursivly setting margin of all grid children, except grids

        public static readonly DependencyProperty SpacingProperty =
            DependencyProperty.RegisterAttached(nameof(FrameworkElement.Margin), typeof(Thickness),
                typeof(GridSpacingSetter), new UIPropertyMetadata(new Thickness(), On_SpacingChanged));

        public static Thickness GetSpacing(DependencyObject obj) => (Thickness)obj.GetValue(SpacingProperty);

        public static void SetSpacing(DependencyObject obj, Thickness value) => obj.SetValue(SpacingProperty, value);

        public static void On_SpacingChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender != null && sender is Grid)
            {
                Grid g = sender as Grid;
                g.Loaded += On_Loaded;
            }
        }

        private static void On_Loaded(object sender, EventArgs e)
        {
            Grid grid = sender as Grid;
            if (grid != null)
            {
                //grid.Loaded += On_Loaded;
                GridSetSpacing(grid, GetSpacing(grid));
            }
        }

        // recursive go through contents
        private static void GridSetSpacing(FrameworkElement element, Thickness margin)
        {
            if (element != null)
            {
                if (element is Grid)
                {
                    Grid grid = element as Grid;

                    foreach (FrameworkElement ch in grid.Children)
                    {
                        GridSetSpacing(ch, margin);
                    }
                }
                else // everything else than grids
                {
                    if (element.Margin.Left == 0 &&
                        element.Margin.Top == 0 &&
                        element.Margin.Right == 0 &&
                        element.Margin.Bottom == 0)
                    {
                        element.Margin = margin;
                    }
                }
            }
        }
    }

    public partial class App : Application
    {
    }
}

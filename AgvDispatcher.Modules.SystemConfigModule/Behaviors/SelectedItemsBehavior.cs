using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace AgvDispatcher.Modules.SystemConfigModule.Behaviors
{
    public static class SelectedItemsBehavior
    {
        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.RegisterAttached(
                "SelectedItems",
                typeof(IList),
                typeof(SelectedItemsBehavior),
                new UIPropertyMetadata(null, OnSelectedItemsChanged));

        public static IList GetSelectedItems(DependencyObject obj)
        {
            return (IList)obj.GetValue(SelectedItemsProperty);
        }

        public static void SetSelectedItems(DependencyObject obj, IList value)
        {
            obj.SetValue(SelectedItemsProperty, value);
        }

        private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ListBox listBox)
            {
                listBox.SelectionChanged -= ListBox_SelectionChanged;

                if (e.NewValue is IList newList)
                {
                    listBox.SelectedItems.Clear();
                    foreach (var item in newList)
                    {
                        listBox.SelectedItems.Add(item);
                    }

                    listBox.SelectionChanged += ListBox_SelectionChanged;

                    if (e.NewValue is INotifyCollectionChanged obsList)
                    {
                        obsList.CollectionChanged += (s, args) =>
                        {
                            listBox.SelectionChanged -= ListBox_SelectionChanged;
                            if (args.Action == NotifyCollectionChangedAction.Reset)
                            {
                                listBox.SelectedItems.Clear();
                            }
                            else
                            {
                                if (args.OldItems != null)
                                {
                                    foreach (var item in args.OldItems) listBox.SelectedItems.Remove(item);
                                }
                                if (args.NewItems != null)
                                {
                                    foreach (var item in args.NewItems) listBox.SelectedItems.Add(item);
                                }
                            }
                            listBox.SelectionChanged += ListBox_SelectionChanged;
                        };
                    }
                }
            }
        }

        private static void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                var boundList = GetSelectedItems(listBox);
                if (boundList != null)
                {
                    foreach (var item in e.RemovedItems)
                    {
                        boundList.Remove(item);
                    }
                    foreach (var item in e.AddedItems)
                    {
                        if (!boundList.Contains(item))
                        {
                            boundList.Add(item);
                        }
                    }
                }
            }
        }
    }
}

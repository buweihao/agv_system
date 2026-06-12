using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using HandyControl.Controls;

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
            if (d is CheckComboBox checkComboBox)
            {
                checkComboBox.SelectionChanged -= CheckComboBox_SelectionChanged;

                if (e.NewValue is IList newList)
                {
                    checkComboBox.SelectedItems.Clear();
                    foreach (var item in newList)
                    {
                        checkComboBox.SelectedItems.Add(item);
                    }

                    checkComboBox.SelectionChanged += CheckComboBox_SelectionChanged;

                    if (e.NewValue is INotifyCollectionChanged obsList)
                    {
                        obsList.CollectionChanged += (s, args) =>
                        {
                            checkComboBox.SelectionChanged -= CheckComboBox_SelectionChanged;
                            if (args.Action == NotifyCollectionChangedAction.Reset)
                            {
                                checkComboBox.SelectedItems.Clear();
                            }
                            else
                            {
                                if (args.OldItems != null)
                                {
                                    foreach (var item in args.OldItems) checkComboBox.SelectedItems.Remove(item);
                                }
                                if (args.NewItems != null)
                                {
                                    foreach (var item in args.NewItems) checkComboBox.SelectedItems.Add(item);
                                }
                            }
                            checkComboBox.SelectionChanged += CheckComboBox_SelectionChanged;
                        };
                    }
                }
            }
        }

        private static void CheckComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is CheckComboBox checkComboBox)
            {
                var boundList = GetSelectedItems(checkComboBox);
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

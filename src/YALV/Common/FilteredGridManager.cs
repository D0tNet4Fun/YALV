using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using YALV.Common.Converters;
using YALV.Core.Domain;
using YALV.Properties;

namespace YALV.Common
{
    public class FilteredGridManager
        : FilteredGridManagerBase
    {
        public static readonly DependencyProperty FilterTextBoxProperty = DependencyProperty.RegisterAttached("FilterTextBox", typeof(TextBox), typeof(DataGridTextColumn));

        public FilteredGridManager(DataGrid dg, Panel txtSearchPanel, KeyEventHandler keyUpEvent)
            : base(dg, txtSearchPanel, keyUpEvent)
        {
            _centerCellStyle = Application.Current.FindResource("CenterDataGridCellStyle") as Style;
            _adjConv = new AdjustValueConverter();
        }

        #region Private Properties

        private Style _centerCellStyle;
        private AdjustValueConverter _adjConv;

        #endregion

        #region Public Methods

        public void BuildDataGrid(IList<ColumnItem> columns)
        {
            if (_dg == null)
                return;

            if (_filterPropertyList == null)
                _filterPropertyList = new List<string>();
            else
                _filterPropertyList.Clear();

            if (columns != null)
            {
                foreach (ColumnItem item in columns)
                {
                    DataGridTextColumn col = new DataGridTextColumn();
                    col.Header = item.Header;
                    if (item.Alignment == CellAlignment.CENTER && _centerCellStyle != null)
                        col.CellStyle = _centerCellStyle;
                    if (item.MinWidth != null)
                        col.MinWidth = item.MinWidth.Value;
                    if (item.Width != null)
                        col.Width = item.Width.Value;

                    Binding bind = new Binding(item.Field) { Mode = BindingMode.OneWay };
                    bind.ConverterCulture = CultureInfo.GetCultureInfo(Resources.CultureName);
                    if (!String.IsNullOrWhiteSpace(item.StringFormat))
                        bind.StringFormat = item.StringFormat;
                    col.Binding = bind;

                    //Add column to datagrid
                    _dg.Columns.Add(col);

                    if (_txtSearchPanel != null)
                    {
                        TextBox txt = new TextBox();
                        Style txtStyle = Application.Current.FindResource("RoundWatermarkTextBox") as Style;
                        if (txtStyle != null)
                            txt.Style = txtStyle;
                        txt.Name = getTextBoxName(item.Field);
                        txt.ToolTip = String.Format(Resources.FilteredGridManager_BuildDataGrid_FilterTextBox_Tooltip, item.Header);
                        txt.Tag = txt.ToolTip.ToString().ToLower();
                        txt.Text = string.Empty;
                        txt.AcceptsReturn = false;
                        txt.SetBinding(TextBox.WidthProperty, BuildOneWayLinkedBinding(col, DataGridColumn.ActualWidthProperty, _adjConv, "-2"));
                        _filterPropertyList.Add(item.Field);
                        if (_keyUpEvent != null)
                            txt.KeyUp += _keyUpEvent;

                        RegisterControl<TextBox>(_txtSearchPanel, txt.Name, txt);
                        _txtSearchPanel.Children.Add(txt);
                        col.SetValue(FilterTextBoxProperty, txt);
                    }
                }
                if (_dg.CanUserReorderColumns)
                {
                    _dg.ColumnReordered += OnColumnReordered;
                }
            }
        }

        #endregion

        #region Private methods

        public void RegisterControl<T>(FrameworkElement element, string controlName, T control)
        {
            if ((T)element.FindName(controlName) != null)
            {
                element.UnregisterName(controlName);
            }
            element.RegisterName(controlName, control);
        }

        private Binding BuildOneWayLinkedBinding(object source, DependencyProperty property, IValueConverter converter = null, object converterParameter = null)
        {
            return new Binding(property.Name)
            {
                Source = source,
                Converter = converter,
                ConverterParameter = converterParameter,
                Mode = BindingMode.OneWay
            };
        }

        private void OnColumnReordered(object sender, DataGridColumnEventArgs e)
        {
            // move the corresponding filter text box along with the column;
            // 'move' means removing the control from its parent stack panel and inserting it at an index that matches the column display index
            var column = e.Column;
            var textBox = (TextBox)column.GetValue(FilterTextBoxProperty);
            _txtSearchPanel.Children.Remove(textBox);
            _txtSearchPanel.Children.Insert(column.DisplayIndex, textBox);
        }

        #endregion

        protected override void OnDispose()
        {
            if (_dg.CanUserReorderColumns)
            {
                _dg.ColumnReordered -= OnColumnReordered;
            }
            base.OnDispose();
        }
    }
}

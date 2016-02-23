using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using YALV.Core.Domain;
using YALV.Core.Filters;
using YALV.Properties;

namespace YALV.Common
{
    public class FilteredGridManagerBase
        : DisposableObject
    {
        public FilteredGridManagerBase(DataGrid dg, Panel txtSearchPanel, KeyEventHandler keyUpEvent)
        {
            _dg = dg;
            _txtSearchPanel = txtSearchPanel;
            _keyUpEvent = keyUpEvent;
            _filterPropertyList = new List<string>();
            _txtCache = new Hashtable();
            IsFilteringEnabled = true;
            _filters = new ObservableCollection<IFilter>();
        }

        protected override void OnDispose()
        {
            ClearCache();
            if (_filterPropertyList != null)
                _filterPropertyList.Clear();
            if (_dg != null)
                _dg.Columns.Clear();
            if (_cvs != null)
            {
                if (_cvs.View != null)
                    _cvs.View.Filter = null;
                BindingOperations.ClearAllBindings(_cvs);
            }
            base.OnDispose();
        }

        #region Private Properties

        protected IList<string> _filterPropertyList;
        protected DataGrid _dg;
        protected Panel _txtSearchPanel;
        protected KeyEventHandler _keyUpEvent;
        protected CollectionViewSource _cvs;
        protected Hashtable _txtCache;
        private readonly ObservableCollection<IFilter> _filters;

        #endregion

        #region Public Methods

        public virtual void AssignSource(Binding sourceBind)
        {
            if (_cvs == null)
                _cvs = new CollectionViewSource();
            else
                BindingOperations.ClearBinding(_cvs, CollectionViewSource.SourceProperty);

            BindingOperations.SetBinding(_cvs, CollectionViewSource.SourceProperty, sourceBind);
            BindingOperations.ClearBinding(_dg, DataGrid.ItemsSourceProperty);
            Binding bind = new Binding() { Source = _cvs, Mode = BindingMode.OneWay };
            _dg.SetBinding(DataGrid.ItemsSourceProperty, bind);
        }

        public void LoadFilters(IEnumerable<IFilter> filters)
        {
            _filters.Clear();
            foreach (var filter in filters)
            {
                filter.Culture = CultureInfo.GetCultureInfo(Resources.CultureName);
                _filters.Add(filter);
            }
        }

        public ICollectionView GetCollectionView()
        {
            if (_cvs != null)
            {
                //Assign filter method
                if (_cvs.View != null && _cvs.View.Filter == null)
                {
                    IsFilteringEnabled = false;
                    _cvs.View.Filter = itemCheckFilter;
                    IsFilteringEnabled = true;
                }
                return _cvs.View;
            }
            return null;
        }

        public void ResetSearchTextBox()
        {
            if (_filterPropertyList != null && _txtSearchPanel != null)
            {
                //Clear all textbox text
                foreach (string prop in _filterPropertyList)
                {
                    TextBox txt = _txtSearchPanel.FindName(getTextBoxName(prop)) as TextBox;
                    if (txt != null & !string.IsNullOrEmpty(txt.Text))
                        txt.Text = string.Empty;
                }
            }
        }

        public void ClearCache()
        {
            if (_txtCache != null)
                _txtCache.Clear();
        }

        public Func<object, bool> OnBeforeCheckFilter;

        public Func<object, bool, bool> OnAfterCheckFilter;

        public bool IsFilteringEnabled { get; set; }

        #endregion

        #region Private Methods

        protected string getTextBoxName(string prop)
        {
            return string.Format("txtFilter{0}", prop).Replace(".", "");
        }

        protected bool itemCheckFilter(object item)
        {
            bool res = true;

            if (!IsFilteringEnabled)
                return res;

            try
            {
                if (OnBeforeCheckFilter != null)
                    res = OnBeforeCheckFilter(item);

                if (!res)
                    return res;

                if (_filterPropertyList != null && _txtSearchPanel != null)
                {
                    //Check each filter property
                    foreach (string prop in _filterPropertyList)
                    {
                        TextBox txt = null;
                        if (_txtCache.ContainsKey(prop))
                            txt = _txtCache[prop] as TextBox;
                        else
                        {
                            txt = _txtSearchPanel.FindName(getTextBoxName(prop)) as TextBox;
                            _txtCache[prop] = txt;
                        }

                        res = false;
                        if (txt == null)
                            res = true;
                        else
                        {
                            if (string.IsNullOrEmpty(txt.Text))
                                res = true;
                            else
                            {
                                try
                                {
                                    //Get property value
                                    object val = getItemValue(item, prop);
                                    if (val != null)
                                    {
                                        string valToCompare = string.Empty;
                                        if (val is DateTime)
                                            valToCompare = ((DateTime)val).ToString(GlobalHelper.DisplayDateTimeFormat, System.Globalization.CultureInfo.GetCultureInfo(Properties.Resources.CultureName));
                                        else
                                            valToCompare = val.ToString();

                                        if (valToCompare.ToString().IndexOf(txt.Text, StringComparison.OrdinalIgnoreCase) >= 0)
                                            res = true;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine(ex.Message);
                                    res = true;
                                }
                            }
                        }
                        if (!res)
                            return res;
                    }

                    if (_filters.Count > 0)
                    {
                        var logItem = (LogItem)item;

                        // pass the item thru every filter;
                        // exclusive filters need to be first; if the item is excluded by one of these then return false;
                        // inclusive filters are next; if the item is explicitly included by one of these then return true;
                        // if at least one inclusive filter decided the item can be excluded and no other inclusive filter has requested the inclusion of the item,
                        // then return false.

                        var canBeExcluded = false;
                        foreach (var filter in _filters.OrderByDescending(f => f.Mode == FilterMode.Exclude))
                        {
                            var filterResult = filter.Apply(logItem);
                            switch (filterResult)
                            {
                                case FilterResult.Exclude:
                                    return false;
                                case FilterResult.CanExclude:
                                    canBeExcluded = true;
                                    break;
                                case FilterResult.Include:
                                    return true;
                            }
                        }
                        if (canBeExcluded)
                        {
                            return false;
                        }
                    }
                }
                res = true;
            }
            finally
            {
                if (OnAfterCheckFilter != null)
                    res = OnAfterCheckFilter(item, res);

            }
            return res;
        }

        protected object getItemValue(object item, string prop)
        {
            object val = null;
            try
            {
                val = item.GetType().GetProperty(prop).GetValue(item, null);
            }
            catch
            {
                val = null;
            }
            return val;
        }

        #endregion
    }

}
using OF.UI.Identity;
using OF.UI.Models.ViewPersistence;
using Infragistics.Web.Mvc;
using System.Reflection;

namespace OF.UI.Grid
{
    public class GridFactory : IGridFactory
    {
        private IUserIdentity _userIdentity;
        private const int DEFAULT_WIDTH_MULT = 14;

        public GridFactory(IUserIdentity userIdentity)
        {
            _userIdentity = userIdentity;
        }

        List<GridColumn> ProcessGridColumns(PersistedView persisted, bool ganttView, List<GridColumn> gridColumns)
        {
            void MergeColumns(Models.ViewPersistence.ColumnSetting column, GridColumn? grid)
            {
                if (grid == null)
                {
                    return;
                }

                var properties = typeof(Models.ViewPersistence.ColumnSetting).GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var gridProperties = typeof(GridColumn).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var property in properties)
                {
                    if (property.Name.ToLower() == "key")
                    {
                        continue;
                    }

                    var gridProperty = gridProperties.FirstOrDefault(i => i.Name.ToLower() == property.Name.ToLower());

                    if (gridProperty == null || !gridProperty.CanWrite)
                    {
                        continue;
                    }

                    var value = property.GetValue(column);

                    if (value == null)
                    {
                        continue;
                    }

                    gridProperty.SetValue(grid, value);
                }
            }

            // Default width
            foreach (var c in gridColumns)
            {
                if (String.IsNullOrEmpty(c.Width))
                {
                    c.Width = ((c.HeaderText.Length) * DEFAULT_WIDTH_MULT).ToString() + "px";
                }
            }

            if (persisted == null || persisted.Columns == null)
            {
                return gridColumns;
            }
            else
            {
                // Reorganise to match the order etc. of the persisted model
                List<GridColumn> reordered = new List<GridColumn>();

                foreach (var columnDef in persisted.Columns)
                {
                    var col = gridColumns.Where(c => c.Key == columnDef.Key).FirstOrDefault();
                    if (col != null)
                    {
                        col.Hidden = columnDef.Hidden;
                        if (col.Key != "Id" && col.Key != "StatusInDateRange" && col.Key != "FulfilmentStatus")
                        {
                            col.Width = columnDef.Width;
                        }

                        reordered.Add(col);
                    }
                }

                // Missing (new columns) - upgrade the view
                foreach (var c in gridColumns)
                {
                    var col = reordered.Where(r => r.Key == c.Key).FirstOrDefault();
                    if (col == null)
                    {
                        reordered.Add(c);
                    }
                }

                // Merge the values from each
                foreach (var p in persisted.Columns)
                {
                    MergeColumns(p, gridColumns.Where(c => c.Key.ToLower() == p.Key.ToLower()).FirstOrDefault());
                }

                return reordered;
            }
        }

        public List<GridColumn> GetTaskGridColumns(PersistedView persisted, bool ganttView)
        {
            var dateFormat = "MM/dd/yyyy";

            if (_userIdentity != null)
            {
                dateFormat = _userIdentity.GetIdentity().DateFormat;
            }

            var gridColumns = new List<GridColumn>();
            gridColumns.Add(new GridColumn() { Key = "Id", HeaderText = "Id", DataType = "number", Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "FulfilmentStatus", HeaderText = "", DataType = "number", Width = "30px", FormatterFunction = "formatFulfilmentStatus" });
            gridColumns.Add(new GridColumn() { Key = "DeliveryDate", HeaderText = "Delivery Date", DataType = "date", Format = dateFormat });
            gridColumns.Add(new GridColumn() { Key = "AgreementNumber", HeaderText = "Agreement", DataType = "string" });
            gridColumns.Add(new GridColumn() { Key = "Division", HeaderText = "Division", DataType = "string", Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "Warehouse", HeaderText = "Warehouse", DataType = "string" });
            gridColumns.Add(new GridColumn() { Key = "FromDate", HeaderText = "From Date", DataType = "date", Format = dateFormat, Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "ToDate", HeaderText = "To Date", DataType = "date", Format = dateFormat, Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "ValidFromDate", HeaderText = "Line Valid From", DataType = "date", Format = dateFormat, Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "ValidToDate", HeaderText = "Line Valid To", DataType = "date", Format = dateFormat, Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "TerminationDate", HeaderText = "Termination Date", DataType = "date", Format = dateFormat, Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "CollectionDate", HeaderText = "Collection Date", DataType = "date", Format = dateFormat, Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "CustomerName", HeaderText = "Customer Name", DataType = "string" });
            gridColumns.Add(new GridColumn() { Key = "CustomerNumber", HeaderText = "Customer Number", DataType = "string", Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "CustomerAddress", HeaderText = "Customer Address", DataType = "string", Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "LineCount", HeaderText = "Line Count", DataType = "number", Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "MinFulfilmentStatus", HeaderText = "LS", DataType = "number", Width = "70px", FormatterFunction = "formatFulfilmentStatus", Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "MaxFulfilmentStatus", HeaderText = "HS", DataType = "number", Width = "70px", FormatterFunction = "formatFulfilmentStatus", Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "LastUpdatedByName", HeaderText = "Assigned To", DataType = "string", Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "LastUpdatedDate", HeaderText = "Updated Date", DataType = "date", Format=dateFormat, Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "OpportunityName", HeaderText = "Opportunity Name", DataType = "string"});
            gridColumns.Add(new GridColumn() { Key = "OpportunityStage", HeaderText = "Stage", DataType = "string", Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "Probability", HeaderText = "Probability", DataType = "string", Hidden = true, FormatterFunction = "formatEffectiveProbability"});

            return ProcessGridColumns(persisted, ganttView, gridColumns);
        }

        public List<GridColumn> GetAssetGridColumns(PersistedView persisted, bool ganttView)
        {
            var dateFormat = "MM/dd/yyyy";

            if (_userIdentity != null)
            {
                dateFormat = _userIdentity.GetIdentity().DateFormat;
            }

            var gridColumns = new List<GridColumn>();
            gridColumns.Add(new GridColumn() { Key = "StatusInDateRange", HeaderText = "", DataType = "string", Hidden=false, Width = "60px", FormatterFunction = "formatAssetStatusCompound" });
            gridColumns.Add(new GridColumn() { Key = "Id", HeaderText = "Asset Id", DataType = "string" });
            gridColumns.Add(new GridColumn() { Key = "Division", HeaderText = "Division", DataType = "string", Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "Warehouse", HeaderText = "Warehouse", DataType = "string", Hidden = false });
            gridColumns.Add(new GridColumn() { Key = "WarehouseName", HeaderText = "Warehouse Name", DataType = "string", Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "Description", HeaderText = "Description", DataType = "string", Hidden = false });
            gridColumns.Add(new GridColumn() { Key = "ItemNumber", HeaderText = "Item Number", DataType = "string", Hidden = false });
            gridColumns.Add(new GridColumn() { Key = "Status", HeaderText = "Status", DataType = "string", Hidden = false });
            gridColumns.Add(new GridColumn() { Key = "AgreementNumber", HeaderText = "Agreement", DataType = "string", Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "DeliveryDate", HeaderText = "Delivery Date", DataType = "date", Format=dateFormat, Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "LineValidTo", HeaderText = "Line Valid To", DataType = "date", Format = dateFormat, Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "LineValidFrom", HeaderText = "Line Valid From", DataType = "date", Format = dateFormat, Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "TerminationDate", HeaderText = "Termination Date", DataType = "date", Format = dateFormat, Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "CollectionDate", HeaderText = "Collection Date", DataType = "date", Format = dateFormat, Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "DaysOffHire", HeaderText = "DoH", DataType = "number", Hidden = false, Width = "60px" });
            gridColumns.Add(new GridColumn() { Key = "CustomerNumber", HeaderText = "Customer Number", DataType = "string", Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "CustomerName", HeaderText = "Customer Name", DataType = "string", Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "Facility", HeaderText = "Facility", DataType = "string", Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "WarehouseLocation", HeaderText = "Warehouse Location", DataType = "string", Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "EstimatedReadyDate", HeaderText = "Ready Date", DataType = "date", Format = dateFormat, Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "ProductGroup", HeaderText = "Product Group", DataType = "string" });
            gridColumns.Add(new GridColumn() { Key = "ProductCategory", HeaderText = "Product Category", DataType = "string" });
            gridColumns.Add(new GridColumn() { Key = "RunHours", HeaderText = "Run Hours", DataType = "number", Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "Size", HeaderText = "Size", DataType = "number", Hidden = true });
            gridColumns.Add(new GridColumn() { Key = "TelemetryStatus", HeaderText = "Telemetry", DataType = "string", Hidden = true, FormatterFunction = "formatARMStatus" });
            gridColumns.Add(new GridColumn() { Key = "Remark", HeaderText = "Remarks", DataType = "string", Hidden = true });

            return ProcessGridColumns(persisted, ganttView, gridColumns);
        }

        GridModel CreateBasicGridModel(PersistedView? view, List<GridColumn> cols, string? textFilter, bool ganttView, List<string> textFilterColumns)
        {
            var model = new GridModel();
            model.ID = "grid";
            model.Width = "100%";
            model.Height = "100%";
            model.PrimaryKey = "Id";
            model.AutoGenerateColumns = false;
            model.AutoGenerateLayouts = false;
            model.EnableUTCDates = true;
            model.RenderCheckboxes = true;
            model.RowVirtualization = true;
            model.AutoCommit = true;
            model.AutofitLastColumn = false;
            model.VirtualizationMode = VirtualizationMode.Continuous;

            model.Columns = cols;
            model.AddClientEvent("onRowRendered", "onRowRendered");
            model.Features.Add(new GridUpdating() {
                 EditMode = GridEditMode.None,
                 EnableAddRow = false,
                 EnableDeleteRow = false,
            });

            var sorting = new GridSorting() { ModalDialogHeight = "500px", Type = OpType.Remote, Mode = SortingMode.Multiple, SortingDialogContainment = "window", ApplySortedColumnCss = false, Persist = true };
            if (view != null && view.Sort != null)
            {
                sorting.ColumnSettings = new List<ColumnSortingSetting>();

                foreach (var def in view.Sort)
                {
                    var target = cols.FirstOrDefault(c => c.Key == def.Key);
                    if (target != null)
                    {
                        sorting.ColumnSettings.Add(new ColumnSortingSetting()
                        {
                            ColumnKey = def.Key,
                            CurrentSortDirection = def.Dir,
                            ColumnIndex = def.Index
                        });
                    }
                }
            }
            model.Features.Add(sorting);

            var filtering = new GridFiltering()
            {
                Type = OpType.Remote,
                Mode = FilterMode.Simple,
                FilterDialogContainment = "window",
                FilterDialogMaxFilterCount = "20",
                FilterDelay = 2000
            };

            if (!String.IsNullOrEmpty(textFilter)) // Text filter from the URL
            {
                foreach (var name in textFilterColumns)
                {
                    filtering.ColumnSettings.Add(new ColumnFilteringSetting()
                    {
                        ColumnKey = name,
                        DefaultExpressions = new List<DefaultFilterExpression>()
                    {
                        new DefaultFilterExpression()
                        {
                            Expression = textFilter,
                            Condition = "contains",
                            Logic = "or"
                        }
                    }
                    });
                }
            }
            else if (view != null && view.Filter != null) // Filters from the view
            {
                foreach (var gc in cols) // For each column
                {
                    var filters = view.Filter.Where(f => f.FieldName == gc.Key); // Find matching filters
                    if (filters.Count() > 0)
                    {
                        var setting = new ColumnFilteringSetting();
                        setting.DefaultExpressions = new List<DefaultFilterExpression>();
                        foreach (var def in filters)
                        {
                            setting.ColumnKey = def.FieldName;
                            var exp = new DefaultFilterExpression()
                            {
                                Expression = def.Expr,
                                Condition = def.Cond,
                                Logic = def.Logic
                            };
                            setting.DefaultExpressions.Add(exp);
                        }
                        filtering.ColumnSettings.Add(setting);
                    }
                }
            }

            // Now add custom filters to strings and dates
            foreach (var gc in cols)
            {
                if (gc.DataType == "date")
                {
                    var setting = filtering.ColumnSettings.FirstOrDefault(s => s.ColumnKey == gc.Key);
                    if (setting == null)
                    {
                        setting = new ColumnFilteringSetting();
                        setting.ColumnKey = gc.Key;
                        filtering.ColumnSettings.Add(setting);
                    }

                    setting.CustomConditions.AddCustomCondition("Empty", new FilteringCustomCondition()
                    {
                        LabelText = "Empty",
                        FilterFunc = "Empty",
                        RequireExpr = false,
                        ExpressionText = "Empty"
                    });

                    setting.CustomConditions.AddCustomCondition("NotEmpty", new FilteringCustomCondition()
                    {
                        LabelText = "NotEmpty",
                        FilterFunc = "NotEmpty",
                        RequireExpr = false,
                        ExpressionText = "Not Empty"
                    });
                }

                if (gc.DataType == "string")
                {
                    var setting = filtering.ColumnSettings.FirstOrDefault(s => s.ColumnKey == gc.Key);
                    if (setting == null)
                    {
                        setting = new ColumnFilteringSetting();
                        setting.ColumnKey = gc.Key;
                        filtering.ColumnSettings.Add(setting);
                    }

                    setting.CustomConditions.AddCustomCondition("Empty", new FilteringCustomCondition()
                    {
                        LabelText = "Empty",
                        FilterFunc = "Empty",
                        RequireExpr = false,
                        ExpressionText = "Empty"
                    });

                    setting.CustomConditions.AddCustomCondition("NotEmpty", new FilteringCustomCondition()
                    {
                        LabelText = "NotEmpty",
                        FilterFunc = "NotEmpty",
                        RequireExpr = false,
                        ExpressionText = "Not Empty"
                    });

                    setting.CustomConditions.AddCustomCondition("StartsWithOneOf", new FilteringCustomCondition()
                    {
                        LabelText = "Starts with one of",
                        FilterFunc = "StartsWithOneOf",
                        RequireExpr = true
                    });

                    setting.CustomConditions.AddCustomCondition("EndsWithOneOf", new FilteringCustomCondition()
                    {
                        LabelText = "Ends with one of",
                        FilterFunc = "EndsWithOneOf",
                        RequireExpr = true
                    });

                    setting.CustomConditions.AddCustomCondition("ContainsOneOf", new FilteringCustomCondition()
                    {
                        LabelText = "Contains one of",
                        FilterFunc = "ContainsOneOf",
                        RequireExpr = true
                    });

                    setting.CustomConditions.AddCustomCondition("EqualsOneOf", new FilteringCustomCondition()
                    {
                        LabelText = "Equals one of",
                        FilterFunc = "EqualsOneOf",
                        RequireExpr = true
                    });

                    setting.CustomConditions.AddCustomCondition("NotContainsOneOf", new FilteringCustomCondition()
                    {
                        LabelText = "Not contains one of",
                        FilterFunc = "NotContainsOneOf",
                        RequireExpr = true
                    });

                    setting.CustomConditions.AddCustomCondition("NotEqualsOneOf", new FilteringCustomCondition()
                    {
                        LabelText = "Not equals one of",
                        FilterFunc = "NotEqualsOneOf",
                        RequireExpr = true
                    });
                }

            }

            model.Features.Add(filtering);

            model.Features.Add(new GridPaging()
            {
                Type = OpType.Remote,
                PageSize = GridConfig.DefaultPageSize,
                PageSizeList = GridConfig.PageSizeOptions.ToList(),
                ShowPageSizeDropDown = true
            });

            return model;
        }

        public GridModel CreateTaskModel(PersistedView? view, string? textFilter, bool ganttView, IDictionary<string, string>? parameters)
        {
            var cols = GetTaskGridColumns(view, ganttView);
            var model = CreateBasicGridModel(view, cols, textFilter, ganttView, new List<string>() { "AgreementNumber", "CustomerName" });

            var filtering = model.Features[model.Features.IndexOf(model.Features.FirstOrDefault(f => f is GridFiltering))] as GridFiltering;

            if (ganttView)
            {
                var fixing = new GridColumnFixing();
                model.Features.Add(fixing);
                fixing.ShowFixButtons = false;
                fixing.ColumnSettings = new List<ColumnFixingSetting>();

                foreach (var col in cols)
                {
                    if (!col.Hidden.HasValue || (col.Hidden.HasValue && !col.Hidden.Value))
                    {
                        fixing.ColumnSettings.Add(new ColumnFixingSetting()
                        {
                            ColumnKey = col.Key,
                            AllowFixing = false,
                            IsFixed = true
                        });
                    }
                }

                model.Columns.Add(new GridColumn() { Key="BarData", HeaderText = "<div id='gridGanttHeader'/>", DataType="string", FormatterFunction= "formatBarAgreement", Width = "3400px" });

                fixing.ColumnSettings.Add(new ColumnFixingSetting()
                {
                    ColumnKey = "BarData",
                    AllowFixing = false,
                    IsFixed = false
                });

                var sorting = model.Features[model.Features.IndexOf(model.Features.FirstOrDefault(f => f is GridSorting))] as GridSorting;
                sorting.ColumnSettings.Add(new ColumnSortingSetting()
                {
                    ColumnKey = "BarData",
                    AllowSorting = false
                });

                filtering.ColumnSettings.Add(new ColumnFilteringSetting()
                {
                    ColumnKey = "BarData",
                    AllowFiltering = false
                });
            }
            else
            {
                var moving = new GridColumnMoving() { ColumnMovingDialogContainment = "window" };
                moving.ColumnSettings = new List<ColumnMovingSetting>();
                moving.ColumnSettings.Add(new ColumnMovingSetting() { ColumnKey = "AgreementNumber", AllowMoving = false });
                moving.ColumnSettings.Add(new ColumnMovingSetting() { ColumnKey = "FulfilmentStatus", AllowMoving = false });
                model.Features.Add(moving);

                var hiding = new GridHiding() { ColumnChooserHeight = "500px", ColumnChooserContainment = "window" };
                hiding.ColumnSettings = new List<ColumnHidingSetting>();
                hiding.ColumnSettings.Add(new ColumnHidingSetting() { ColumnKey = "Id", AllowHiding = false });
                hiding.ColumnSettings.Add(new ColumnHidingSetting() { ColumnKey = "AgreementNumber", AllowHiding = false });
                hiding.ColumnSettings.Add(new ColumnHidingSetting() { ColumnKey = "FulfilmentStatus", AllowHiding = false });
                model.Features.Add(hiding);
            }

            var agreementNumberColumnFilteringSetting = filtering.ColumnSettings.FirstOrDefault(cs => cs.ColumnKey == "AgreementNumber");
            if (agreementNumberColumnFilteringSetting is null)
            {
                agreementNumberColumnFilteringSetting = new ColumnFilteringSetting()
                {
                    ColumnKey = "AgreementNumber",
                };
                filtering.ColumnSettings.Add(agreementNumberColumnFilteringSetting);
            }
            agreementNumberColumnFilteringSetting.FilterCondition = "equals";

            var resizing = new GridResizing() { AllowDoubleClickToResize = true };
            resizing.ColumnSettings = new List<ColumnResizingSetting>();
            resizing.ColumnSettings.Add(new ColumnResizingSetting() { ColumnKey = "FulfilmentStatus", AllowResizing = false });
            resizing.ColumnSettings.Add(new ColumnResizingSetting() { ColumnKey = "MaxFulfilmentStatus", AllowResizing = false });
            resizing.ColumnSettings.Add(new ColumnResizingSetting() { ColumnKey = "MinFulfilmentStatus", AllowResizing = false });
            if (ganttView) resizing.ColumnSettings.Add(new ColumnResizingSetting() { ColumnKey = "BarData", AllowResizing = false });
            model.Features.Add(resizing);

            model.Features.Add(new GridSelection() { MultipleSelection = false, AllowMultipleRangeSelection = false, Mode = SelectionMode.Row });

            var tooltips = new GridTooltips();
            tooltips.ColumnSettings = new List<ColumnTooltipsSetting>();
            if (ganttView) tooltips.ColumnSettings.Add(new ColumnTooltipsSetting() { ColumnKey = "BarData", AllowTooltips = false });
            model.Features.Add(tooltips);


            var dsUrl = "/TaskView/GetTasks?";
            if (parameters!= null)
            {
                foreach (var k in parameters.Keys)
                {
                    dsUrl = dsUrl + "&" + k + "=" + parameters[k];
                }
            }
            model.DataSourceUrl = dsUrl;
            model.ResponseDataKey = "Records";

            return model;
        }

        public GridModel CreateAssetModel(PersistedView? view, string? textFilter, bool ganttView, IDictionary<string, string>? parameters)
        {
            var cols = GetAssetGridColumns(view, ganttView);
            var model = CreateBasicGridModel(view, cols, textFilter, ganttView, new List<string>() { "Id", "Description", "ItemNumber" });

            var filtering = model.Features[model.Features.IndexOf(model.Features.FirstOrDefault(f => f is GridFiltering))] as GridFiltering;

            if (ganttView)
            {
                var fixing = new GridColumnFixing();
                model.Features.Add(fixing);

                var hiding = new GridHiding() { ColumnChooserHeight = "500px", ColumnChooserContainment = "window" };
                hiding.ColumnSettings.Add(new ColumnHidingSetting() { ColumnKey = "StatusInDateRange", AllowHiding = false });
                model.Features.Add(hiding);

                model.Features.Add(new GridUpdating()
                {
                    EditMode = GridEditMode.None,
                    EnableAddRow = false,
                    EnableDeleteRow = false,
                });

                fixing.ShowFixButtons = false;
                fixing.ColumnSettings = new List<ColumnFixingSetting>();

                foreach (var col in cols)
                {
                    if (!col.Hidden.HasValue || (col.Hidden.HasValue && !col.Hidden.Value))
                    {
                        fixing.ColumnSettings.Add(new ColumnFixingSetting()
                        {
                            ColumnKey = col.Key,
                            AllowFixing = false,
                            IsFixed = true
                        });
                    }
                }

                model.Columns.Add(new GridColumn() { Key = "BarData", HeaderText = "<div id='gridGanttHeader'/>", DataType = "string", FormatterFunction = "formatBarAsset", Width = "3400px" });

                fixing.ColumnSettings.Add(new ColumnFixingSetting()
                {
                    ColumnKey = "BarData",
                    AllowFixing = false,
                    IsFixed = false
                });

                fixing.ColumnSettings.Add(new ColumnFixingSetting()
                {
                    ColumnKey = "StatusInDateRange",
                    AllowFixing = false,
                    IsFixed = false
                });

                var sorting = model.Features[model.Features.IndexOf(model.Features.FirstOrDefault(f => f is GridSorting))] as GridSorting;
                sorting.ColumnSettings.Add(new ColumnSortingSetting()
                {
                    ColumnKey = "BarData",
                    AllowSorting = false
                });

                filtering.ColumnSettings.Add(new ColumnFilteringSetting()
                {
                    ColumnKey = "StatusInDateRange",
                    AllowFiltering = false
                });
                filtering.ColumnSettings.Add(new ColumnFilteringSetting()
                {
                    ColumnKey = "BarData",
                    AllowFiltering = false
                });
            }
            else
            {
                var moving = new GridColumnMoving() { ColumnMovingDialogContainment = "window" };
                moving.ColumnSettings.Add(new ColumnMovingSetting() { ColumnKey = "StatusInDateRange", AllowMoving = false });
                model.Features.Add(moving);

                var hiding = new GridHiding() { ColumnChooserHeight = "500px", ColumnChooserContainment = "window" };
                hiding.ColumnSettings.Add(new ColumnHidingSetting() { ColumnKey = "StatusInDateRange", AllowHiding = false });
                model.Features.Add(hiding);
            }

            var idColumnFilteringSetting = filtering.ColumnSettings.FirstOrDefault(cs => cs.ColumnKey == "Id");
            if (idColumnFilteringSetting is null)
            {
                idColumnFilteringSetting = new ColumnFilteringSetting()
                {
                    ColumnKey = "Id",
                };
                filtering.ColumnSettings.Add(idColumnFilteringSetting);
            }
            idColumnFilteringSetting.FilterCondition = "equals";

            var resizing = new GridResizing() { AllowDoubleClickToResize = true };
            resizing.ColumnSettings = new List<ColumnResizingSetting>();
            resizing.ColumnSettings.Add(new ColumnResizingSetting() { ColumnKey = "StatusInDateRange", AllowResizing = false });
            if (ganttView) resizing.ColumnSettings.Add(new ColumnResizingSetting() { ColumnKey = "BarData", AllowResizing = false });
            model.Features.Add(resizing);

            model.Features.Add(new GridSelection() { MultipleSelection = true, AllowMultipleRangeSelection = false, Mode = SelectionMode.Row });

            var tooltips = new GridTooltips();
            tooltips.ColumnSettings = new List<ColumnTooltipsSetting>();
            if (ganttView) tooltips.ColumnSettings.Add(new ColumnTooltipsSetting() { ColumnKey = "BarData", AllowTooltips = false });
            model.Features.Add(tooltips);

            var dsUrl = "/TaskView/GetAssets?";
            if (parameters != null)
            {
                foreach (var k in parameters.Keys)
                {
                    dsUrl = dsUrl + "&" + k + "=" + parameters[k];
                }
            }
            model.DataSourceUrl = dsUrl;
            model.ResponseDataKey = "Records";

            return model;
        }
    }
}

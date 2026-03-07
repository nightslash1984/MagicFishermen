using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Properties;
using UnityEditor.U2D.Tooling.Analyzer.UIElement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.U2D.Sprites;

namespace UnityEditor.U2D.Tooling.Analyzer
{
    class AtlasPageIssue : AnalyzerIssueReportBase
    {
        [Serializable]
        record AtlasPageIssueRecord
        {
            public EditorAtlasInfo atlasInfo;
            public int pageCount;
        }

        CommonSpriteAtlasIssueView m_View;
        SpriteAtlasReportTable m_Table;
        List<EditorAtlasInfo> m_Data;
        List<AtlasPageIssueRecord> m_Filtered = new();
        Column[] m_Columns;
        int m_PageCont = 1;
        AtlasPageSettings m_Settings;


        public AtlasPageIssue(): base(new [] {typeof(SpriteAtlasDataSource)})
        {
            m_View = new CommonSpriteAtlasIssueView();
            m_View.styleSheets.Add(CommonStyleSheet.iconStyleSheet);

            SetReportListItemName();
            SetReportListemCount("0");
            m_View.ShowTable(false, "Analyze has not been done yet.");

            m_Settings = new AtlasPageSettings(m_PageCont);
            m_Settings.pageCountChanged += OnSettingsPageCountChanged;
            m_Table = m_View.table;
            m_Table.sortingMode = ColumnSortingMode.Default;
            m_Table.AddManipulator(new ContextualMenuManipulator(OnContextualMenuManipulator));
            table.selectionChanged += OnSelectionChanged;
            SetupColumns();
        }

        void OnSelectionChanged(IEnumerable<object> obj)
        {
            InspectObject(m_Filtered[table.selectedIndex].atlasInfo.GetObject());
        }

        void OnContextualMenuManipulator(ContextualMenuPopulateEvent obj)
        {
            var menuStatus = m_Filtered.Count > 0 && table.selectedIndex >= 0 &&
                table.selectedIndex < table.itemsSource.Count ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
            obj.menu.AppendAction("Reanalyze Atlas", (a) => RecheckAtlas(),
                menuStatus);
        }

        void RecheckAtlas()
        {
            var item = m_Filtered[table.selectedIndex].atlasInfo.GetObject();
            if (item != null)
            {
                RequestCapture(new [] {AssetDatabase.GetAssetPath(item)});
            }
        }

        void SetReportListItemName()
        {
            SetReportListItemName($"Atlas Page Count > {m_PageCont}");
        }

        void SetupColumns()
        {
            m_Columns = new []
            {
                new Column
                {
                    title = "Name",
                    width = Length.Pixels(80),
                    sortable = true
                }, new Column
                {
                    title = "Page Count",
                    width = Length.Pixels(80),
                    sortable = true
                }
            };

            m_Columns[0].makeCell = () =>
            {
                var ele = new CellLabelWithIcon();
                ele.SetIconClassName("spriteatlas-icon");
                return ele;
            };
            m_Columns[0].bindCell = (e, i) =>
            {
                (e as CellLabelWithIcon).BindLabel(new DataBinding
                {
                    dataSourcePath = new PropertyPath("atlasInfo.m_Name"),
                });
                e.dataSource = m_Filtered[i];
            };
            m_Columns[0].comparison = (a, b) =>
            {
                var itemA = m_Filtered[a];
                var itemB = m_Filtered[b];
                return string.Compare(itemA.atlasInfo.name.ToLower(), itemB.atlasInfo.name.ToLower(), StringComparison.Ordinal);
            };
            m_Columns[1].makeCell = () =>
            {
                var ele = new CellLabelWithIcon();
                return ele;
            };
            m_Columns[1].bindCell = (e, i) =>
            {
                (e as CellLabelWithIcon).BindLabel(new DataBinding
                {
                    dataSourcePath = new PropertyPath(""),
                });
                e.dataSource = m_Filtered[i].pageCount;
            };
            m_Columns[1].comparison = (a, b) =>
            {
                var itemA = m_Filtered[a].pageCount;
                var itemB = m_Filtered[b].pageCount;
                return itemA.CompareTo(itemB);
            };
            for (int i = 0; i < m_Columns.Length; ++i)
                table.columns.Add(m_Columns[i]);
        }

        MultiColumnListView table => m_Table.multiColumnListView;

        public override VisualElement reportContent => m_View;
        public override VisualElement settingsContent => m_Settings;
        public override string reportTitle => "Atlas Texture Pages";

        protected override async void OnReportDataSourceChanged(IReportDataSource reportDataSource)
        {
            if (reportDataSource is SpriteAtlasDataSource dataSource)
            {
                await SetDataSourceProvider(dataSource);
            }
        }

        public async Task SetDataSourceProvider(SpriteAtlasDataSource dataSource)
        {
            if (dataSource?.data != null)
            {
                await SetDataSource(dataSource.data);
            }
        }

        async Task SetDataSource(List<EditorAtlasInfo> dataSource)
        {
            if (dataSource == null)
                return;
            m_Data = dataSource;
            isFilteringReport = true;
            m_View.ShowTable(false, "Filtering data in progress...");
            m_Filtered = await FilterDataAsync(m_Data);
            table.itemsSource = m_Filtered;
            table.Rebuild();
            isFilteringReport = false;
            SetReportListemCount($"{m_Filtered.Count}");
            m_View.ShowTable(m_Filtered.Count > 0, $"No Sprite Atlas with pages greater than {m_PageCont} found.");
        }

        async Task<List<AtlasPageIssueRecord>> FilterDataAsync(List<EditorAtlasInfo> dataSource)
        {
            var result = new List<AtlasPageIssueRecord>();
            SpriteDataProviderFactories spriteDataFactory = new();
            var uniqueDataPath = new HashSet<string>();
            for(int i = 0; i < dataSource.Count; ++i)
            {
                uniqueDataPath.Clear();
                var atlasInfo = dataSource[i];
                var expectedPageCount = 1;
                for (int j = 0; j < atlasInfo?.spriteInfo.Count; ++j)
                {
                    await Task.Delay(10);
                    var si = atlasInfo.spriteInfo[j];

                    if (!uniqueDataPath.Contains(si.assetPath))
                    {
                        var sprite = AssetDatabase.LoadMainAssetAtPath(si.assetPath);
                        if (sprite != null)
                        {
                            var dp = spriteDataFactory.GetSpriteEditorDataProviderFromObject(sprite);
                            dp.InitSpriteEditorDataProvider();
                            var stdp = dp.GetDataProvider<ISecondaryTextureDataProvider>();
                            uniqueDataPath.Add(si.assetPath);
                            expectedPageCount = Math.Max(expectedPageCount, (stdp?.textures?.Length ?? 0) + 1);
                        }
                    }
                }

                if ((atlasInfo.textureInfo == null && atlasInfo.spriteInfo.Count >0) ||
                    (atlasInfo.textureInfo != null && (atlasInfo.textureInfo.Count/expectedPageCount) > m_PageCont))
                {
                    result.Add(new AtlasPageIssueRecord()
                    {
                        atlasInfo = atlasInfo,
                        pageCount = atlasInfo.textureInfo.Count / expectedPageCount
                    });
                }
            }
            return result;
        }

        async void OnSettingsPageCountChanged(int obj)
        {
            m_PageCont = obj;
            SetReportListItemName();
            await SetDataSource(m_Data);
        }
    }
}

var exportingIndicator = $('<div>');

$(document).ready(function () {
    $("#btnExport").click(function () {
        var currentDate = new Date().toISOString().slice(0, 10); 
        var fileName = `NOF_${pageView}_Data_${currentDate}`;
        $.ig.GridExcelExporter.exportGrid($("#grid"), {
            fileName: fileName,
            gridFeatureOptions: { "gridStyling": "applied", "sorting": "applied", "filtering": "applied", "paging": "allRows", "hiding": "visibleColumnsOnly" },
            columnsToSkip: ["StatusInDateRange","FulfilmentStatus", "BarData"]
        },
            {
                exportStarting: function (e, args) {
                    showExportingIndicator(args.grid, exportingIndicator);
                },
                success: function () {
                    hideExportingIndicator(exportingIndicator);
                }
            });
    });
});

function showExportingIndicator(grid, exportingIndicator) {
    var $gridContainer = $('#' + grid.attr('id') + '_container');

    exportingIndicator.css({
        "width": $gridContainer.outerWidth(),
        "height": $gridContainer.outerHeight()
    }).html('<span class="exporting-text">Exporting...</span>');
    exportingIndicator.addClass("exporting-indicator");

    $gridContainer.append(exportingIndicator);
}

function hideExportingIndicator(exportingIndicator) {
    exportingIndicator.remove();
}
function saveGridChanges() {
    $('#grid').igGrid("saveChanges", function (data) {
        showSaveSuccess();
    },
    function (jqXHR, textStatus, errorThrown) {
        showSaveError();
    });
}

var gridSettings = {};

gridSettings.checkSaveValid = function () {
    if ($('#txtViewTitle').val().trim().length > 0) {
        $("#btnSave").removeAttr('disabled');
    } else {
        $("#btnSave").attr('disabled', 'disabled');
    }
}

gridSettings.delete = function () {
    var urlParams = new URLSearchParams(document.location.search);
    var url = '/api/view';
    if (urlParams.has('viewid')) {
        url = url + '?id=' + urlParams.getAll('viewid')[0];

        showConfirm('Confirm',
            'Do you really want to delete this view?',
            function () {
                    $.ajax({
                        type: 'DELETE',
                        url: url,
                        beforeSend: function (xhr) {
                            xhr.setRequestHeader('XSRF-TOKEN',
                                $('input:hidden[name="__RequestVerificationToken"]').val());
                        },
                        success: function () {
                            document.location = '/';
                        },
                        error: function () {
                            showSaveError();
                        }
                    })
                },
            function () {
            }
        );
       
    }
}

gridSettings.editMode = function () {
    $('#container_view_title').attr('style', 'display:none!important');
    $('#container_view_buttons').attr('style', 'display:none!important');
    $('#container_edit_title').show();
    $('#container_edit_buttons').show();
    $('#pfSelectorBar').hide();
}

gridSettings.save = function () {

    var settings = {};

    settings.columns = [];
    var cols = $('#grid').igGrid('option', 'columns');
    for (var i = 0; i < cols.length; i++) {
        var c = cols[i];
        settings.columns.push({
            "key": c.key,
            "hidden": c.hidden,
            "width": c.width
        });
    }

    settings.filter = $('#grid').data('igGrid').dataSource.settings.filtering.expressions;

    settings.sort = [];
    var sortcols = $("#grid").data("igGrid").dataSource.settings.sorting.expressions
    for (var i = 0; i < sortcols.length; i++) {
        var c = sortcols[i];
        if (c.isSorting) {
            settings.sort.push({
                "index": i,
                "key": c.fieldName,
                "dir": c.dir
            });
        }
    }

    try {
        settings.group = [];
        var groupCols = $('#grid').igGridGroupBy('groupByColumns');
        for (var i = 0; i < groupCols.length; i++) {
            var c = groupCols[i];
            settings.group.push({
                "key": c.key,
                "dir": c.dir,
                "layout": c.layout
            });
        }
    }catch (e) { }

    var title = $('#txtViewTitle').val();
    var forEveryone = 0;
    if ($('#chkViewEveryone').is(':checked')) forEveryone = 1;
    if ($('#chkViewDivision').is(':checked')) forEveryone = 2;

    var ganttView = $('#chkGanttView').is(':checked');
    var assetView = $('#chkAssetView').is(':checked');


    var urlParams = new URLSearchParams(document.location.search);
    var url = '/api/view?name=' + title + '&forEveryone=' + forEveryone + '&ganttView=' + ganttView + '&assetView=' + assetView;
    if (urlParams.has('viewid')) {
        url = url + '&id=' + urlParams.getAll('viewid')[0];
    }

    $.ajax({
        type: 'PUT',
        url: url,
        data: JSON.stringify(settings),
        beforeSend: function (xhr) {
            xhr.setRequestHeader('XSRF-TOKEN',
                $('input:hidden[name="__RequestVerificationToken"]').val());
        },
        contentType: 'application/json; charset=utf-8',
        dataType: 'json',
        success: function (response) {
            document.location = '/TaskView?viewid=' + response.id;
        },
        error: function (response) {
            showSaveError();
        }
    });
}

$(document).ready(function () {

    $('#txtViewTitle').keyup(function () {
        gridSettings.checkSaveValid();
    });

    $("#btnSave").click(function () {

        gridSettings.save();
    });

    $("#btnEdit").click(function () {

        gridSettings.editMode();
    });

    $("#btnDelete").click(function () {

        gridSettings.delete();
    });

    $("#btnCancel").click(function () {

        let urlParams = new URLSearchParams(window.location.search);
        if (urlParams.has('viewid')) {
            document.location = '/TaskView?viewid=' + urlParams.getAll('viewid')[0];
        } else {
            document.location = '/TaskView?viewid=1';
        }
    });

    gridSettings.checkSaveValid();

    if (typeof cloneId != "undefined" && cloneId > 0) {

        gridSettings.editMode();

    }
});

$(document).on("iggridsortingcolumnsorting", "#grid", function (evt, ui) {

    // Set single sort mode
    $("#grid").igGridSorting("option", "mode", "single");

    // Remove all multiple sort filters
    for (var i = 0; i < ui.newExpressions.length; i++) {
        var exp = ui.newExpressions[i];
        if (!exp.isGroupBy && exp.fieldName != ui.columnKey) $("#grid").igGridSorting("unsortColumn", exp.fieldName);
    }

    // Sort the new column
    $("#grid").igGridSorting("sortColumn", ui.columnKey, ui.direction);

    // Cancel the original event
    return false;
});

var enableRingFenceSelection = false;

$(document).ready(function () {

    sizeCards();
    DatePickers();
    EventHandlers();
    createMode();
});

$(window).on("resize", function () {

    sizeCards();

});

function sizeCards() {

    var ref = $('#card_ringfences');
    $('#card_ringfences').height($(window).height() - ref.position().top - 20);
    ref = $('#card_ringfence_items');
    $('#card_ringfence_items').height($(window).height() - ref.position().top - 10);
    ref = $('#panel_ringfence_item');
    $('#panel_ringfence_item').height($(window).height() - ref.position().top - 200);
};

function refreshDataCallback() {
    document.location = document.location;
}

function hideRingfencePanels() {
    $('.ringfence-panel').hide();
}

function createMode() {
    $('#btnUpdateRingfence').hide();
    $('#btnCancel').hide();
    $('#btnCreateRingfence').show();
    $('#actionTitle').html('&nbsp;Create Ringfence');

    let startDate = new Date();
    startDate.setDate(startDate.getDate() + 1);
    let endDate = new Date();
    endDate.setDate(startDate.getDate() + 6);

    $('#ringFenceFromDate').igDatePicker("option", "value", startDate);
    $('#ringFenceToDate').igDatePicker("option", "value", endDate);

    $('#ringfenceId').val(null);
    $('#ringFenceName').igTextEditor('value', null);
    $('#ringFenceDivision').igCombo('value', null);
    $('#ringFenceWarehouse').igCombo('value', null);
    $('#ringFenceOwner').igCombo('value', null);
}

function inEditMode() {
    return $('#btnUpdateRingfence').is(":visible");
}

function DatePickers() {
    var today = new Date(),
        tomorrow = new Date(new Date().getTime() + 24 * 60 * 60 * 1000);

    $("#ringFenceFromDate").igDatePicker({
        regional: "en-GB",
        placeholder: "Start Date",
        dateDisplayFormat: dateFormat,
        datepickerOptions: {
            minDate: today
        }
    });

    $("#ringFenceToDate").igDatePicker({
        regional: "en-GB",
        placeholder: "End Date",
        dateDisplayFormat: dateFormat,
        datepickerOptions: {
            minDate: tomorrow
        }
    });
}

function EventHandlers() {
    let loginName = $("#user-login-name").val();
    let loginDivisions = $('#user-divisions').val().split(',');

    $('#btnCancel').click(function () {
        createMode();

        let grid = $("#ringFences");
        grid.fadeTo(500, 1);
        grid.find("input,button,textarea,select").removeAttr("disabled");

        return false;
    });

    $('#ringFenceName')
        .igTextEditor({
            width: "100%",
            selectionOnFocus: "atEnd",
            placeHolder: "Ringfence Name"
        }).change(function () {
            pf_validateRingfence();
        });

    $('#ringFenceFromDate').igDatePicker({
        width: "100%",
        valueChanged: function (evt, ui) {
            pf_validateRingfence();
            if (ui.newValue instanceof Date) {
                var nextDay = new Date(ui.newValue.getTime() + 24 * 60 * 60 * 1000);
                var currentValue = $("#ringFenceToDate").igDatePicker("option", "value");
                if (currentValue.getTime() > nextDay.getTime()) {
                    $("#ringFenceToDate").igDatePicker("option", "datepickerOptions", {
                        minDate: nextDay
                    }).igDatePicker("option", "value", nextDay);
                } else {
                    $("#ringFenceToDate").igDatePicker("option", "value", nextDay).igDatePicker("option", "datepickerOptions", {
                        minDate: nextDay
                    });
                }
            }
        }
    });

    $('#ringFenceToDate').igDatePicker({
        width: "100%",
        valueChanged: function (evt, ui) {
            pf_validateRingfence();
        }
    });

    $("#ringFenceOwner").igCombo({
        width: "100%",
        textKey: "loginName",
        valueKey: "loginName",
        allowCustomValue: false,
        locale: {
            placeHolder: "Select owner"
        },
        dropDownOrientation: "bottom",
        selectionChanged: function (evt, ui) {
            pf_validateRingfence();
        }
    });

    $("#ringFenceDivision").igCombo({
        width: "100%",
        dataSource: loginDivisions,
        textKey: "code",
        valueKey: "code",
        delayInputChangeProcessing: 200,
        locale: {
            placeHolder: "Select division"
        },
        dropDownOrientation: "bottom",
        selectionChanged: function (evt, ui) {
            if (ui.items && ui.items[0]) {
                bindBySelectedDivision(ui.items[0].data.code, loginName);
            }

            pf_validateRingfence();
        }
    });

    $("#ringFenceWarehouse").igCombo({
        width: "100%",
        textKey: "warehouse",
        valueKey: "warehouseCode",
        locale: {
            placeHolder: "Select warehouse"
        },
        dropDownOrientation: "bottom",
        itemTemplate: "${warehouseCode}: ${warehouse}",
        selectionChanged: function (evt, ui) {
            pf_validateRingfence();
        }
    });

    $('#btnCreateRingfence').click(function () {
        pf_createRingfence();
        return false;
    });

    $('#btnUpdateRingfence').click(function () {
        pf_saveRingfence();
        return false;
    });

    $('#ringFences').on('iggridselectionrowselectionchanging', function () {
        return enableRingFenceSelection;
    });
}

function getUserListItems(division){
    if (divisions?.length) {
        return $.ajax({
            type: "GET",
            url: "/api/Task/UsersByDivision/" + division,
            contentType: "application/json"
        });
    }
    return Promise.resolve([]);
}

function getWarehouseListItems(division) {
    if (division) {
        return $.ajax({
            type: "GET",
            url: "/api/FulfilmentEngine/GetWarehousesByDivision/" + division,
            contentType: "application/json"
        });
    }
    return Promise.resolve([]);
}

function pf_validateRingfence() {
    let isValidForCreate = (
        validDateRangeSelected() &&
        inputHasValue('ringFenceName') &&
        comboHasValue('ringFenceOwner') &&
        comboHasValue('ringFenceDivision') &&
        comboHasValue('ringFenceWarehouse'));

    if (isValidForCreate) {
        $('#btnCreateRingfence').prop('disabled', false);
        $('#btnUpdateRingfence').prop('disabled', false);
    } else {
        $('#btnCreateRingfence').prop('disabled', true);
        $('#btnUpdateRingfence').prop('disabled', true);
    }
}

function validDateRangeSelected() {
    let fromDate = $('#ringFenceFromDate').igDatePicker('value');
    let toDate = $('#ringFenceToDate').igDatePicker('value');

    if (fromDate == null || toDate == null) {
        return false;
    }

    if (fromDate > toDate) {

        return false;
    }

    return true;
}

function inputHasValue(inputId) {
    let inputValue = $('#' + inputId).val();
    return hasValue(inputValue);
}

function comboHasValue(inputId) {
    let inputValue = $('#' + inputId).igCombo('value');
    return hasValue(inputValue);
}

function hasValue(value) {
    if (value == null || value == undefined) {
        return false;
    }

    if (Array.isArray(value)) {
        return value.length > 0;
    }

    return value != null && value.trim() != '';
}

function buildRequestModel() {
    let request = {};
    request.startDate = $('#ringFenceFromDate').igDatePicker('value').toJSON();
    request.endDate = $('#ringFenceToDate').igDatePicker('value').toJSON();
    request.name = $('#ringFenceName').val();
    request.owner = $('#ringFenceOwner').igCombo('value');
    request.division = $('#ringFenceDivision').igCombo('value');
    request.warehouse = $('#ringFenceWarehouse').igCombo('value');

    return request;
}

function bindBySelectedDivision(selectedDivi, ownerVal, warehouseVal) {
    if (!selectedDivi) {
        return;
    }
    getWarehouseListItems(selectedDivi).then(function (data) {
        let cboWh = $("#ringFenceWarehouse");
        cboWh.igCombo("deselectAll", {}, true);
        cboWh.igCombo("option", "dataSource", data);
        cboWh.igCombo("dataBind");

        if (warehouseVal) {
            cboWh.igCombo("value", warehouseVal);
        }
    });

    getUserListItems(selectedDivi).then(function (data) {
        let cboOwner = $("#ringFenceOwner");
        cboOwner.igCombo("option", "dataSource", data);
        cboOwner.igCombo("dataBind");

        if (ownerVal) {
            cboOwner.igCombo("value", ownerVal);
        }
    });
}

function pf_createRingfence() {
    if (inEditMode()) {
        return;
    }

    let request = buildRequestModel();

    $('#headerRefresh').show();
    $.ajax({
        type: "POST",
        url: "/api/FulfilmentEngine/CreateRingfence",
        data: JSON.stringify(request),
        contentType: "application/json",
        success: function (data) {
            $('#headerRefresh').hide();
            if (data.isSuccess) {
                document.location = document.location;
            } else {
                showError(data.errorMessage);
            }
        }
    });

}

function pf_highlightRow(id) {
    let grid = $("#ringFences");
    enableRingFenceSelection = true;
    grid.igGridSelection("clearSelection");
    grid.igGridSelection("selectRowById", id);
    enableRingFenceSelection = false;
}

function pf_deleteRingfence(id) {
    pf_highlightRow(id);

    // Clear the ringfenced assets table.
    hideRingfencePanels();

    showConfirm('Confirm',
        'This will delete the ringfence and release all assets. Continue?',
        function () {
            pf_continueDeleteRingfence(id);
        },
        function () {
        }
    );
}

function pf_continueDeleteRingfence(id) {
    var request = {};
    request.id = id;

    $('#headerRefresh').show();

    $.ajax({
        type: "DELETE",
        url: "/api/FulfilmentEngine/DeleteRingfence",
        data: JSON.stringify(request),
        contentType: "application/json",
        success: function (data) {
            $('#headerRefresh').hide();
            if (data.isSuccess) {
                document.location = document.location;
            } else {
                showError(data.errorMessage);
            }
        }
    });
}

function pf_editRingfence(id) {
    pf_highlightRow(id);
    let minDate = new Date('0001-01-01T00:00:00Z');
    let grid = $("#ringFences");

    // Fade the grid out to focus the user to the edit form.
    grid.fadeTo(500, 0.2);
    grid.find("input,button,textarea,select").attr("disabled", "disabled");

    // Clear the ringfenced assets table.
    hideRingfencePanels();

    // Update state
    $('#actionTitle').html('&nbsp;Edit Ringfence');
    $('#btnUpdateRingfence').show();
    $('#btnCancel').show();
    $('#btnCreateRingfence').hide();

    // Binding
    let record = $("#ringFences").igGrid("findRecordByKey", id);
    $('#ringfenceId').val(id);
    $('#ringFenceName').igTextEditor('value', record.Title);
    $('#ringFenceFromDate').igDatePicker({
        minValue: minDate,
        value: record.FromDate,
    });

    $('#ringFenceToDate').igDatePicker({
        minValue: minDate,
        value: record.ToDate,
    });

    if (record.Divisions) {
        let selectedDivi = record.Divisions.split(',')[0];

        $('#ringFenceDivision').igCombo('value', selectedDivi);
        bindBySelectedDivision(selectedDivi, record.Owner, record.Warehouse);
    }

    // And revalidate
    pf_validateRingfence();
}

function pf_saveRingfence() {
    if (!inEditMode()) {
        return;
    }

    let request = buildRequestModel();
    request.Id = $('#ringfenceId').val();

    $('#headerRefresh').show();

    $.ajax({
        type: "POST",
        url: "/api/FulfilmentEngine/EditRingfence",
        data: JSON.stringify(request),
        contentType: "application/json",
        success: function (data) {
            $('#headerRefresh').hide();
            if (data.isSuccess) {
                refreshDataCallback();

            } else {
                showError(data.errorMessage);
            }
        }
    });
}

function pf_selectRingfence(id) {

    $('#headerRefresh').show();

    pf_highlightRow(id);

    $.ajax({
        type: "GET",
        url: "/api/FulfilmentEngine/GetRingfenceAssets?ringfenceId="+id,
        success: function (data) {
            $('#headerRefresh').hide();
            if (data.isSuccess) {
                pf_drawAssets(id, data.assets);
            } else {
                showError(data.errorMessage);
            }
        }
    });
}

function pf_drawAssets(ringfenceId, assets) {

    hideRingfencePanels();

    if (assets.length > 0) {
        $('#panel_ringfence_item').show();
        $('#panel_ringfence_item').empty();

        var table = $('<table class="table table-striped table-hover table-sm"></table>');
        var thead = $('<thead></thead>');
        var theadr = $('<tr></tr>');
        thead.append(theadr);
        table.append(thead);
        theadr.append('<th>Asset ID</th>');
        theadr.append('<th>Division</th>');
        theadr.append('<th>Warehouse</th>');
        theadr.append('<th>Description</th>');
        theadr.append('<th>Item Number</th>');
        theadr.append('<th>Status</th>');
        theadr.append('<th>Whs Location</th>');
        theadr.append('<th style="width: 30px;"></th>');

        for (var i = 0; i < assets.length; i++) {
            var asset = assets[i];
            var tr = $('<tr></tr>');
            table.append(tr);
            tr.append('<td>' + asset.id + '</td>');
            tr.append('<td>' + asset.division + '</td>');
            tr.append('<td>' + asset.warehouse + '</td>');
            tr.append('<td>' + asset.description + '</td>');
            tr.append('<td>' + asset.itemNumber + '</td>');
            tr.append('<td>' + asset.status + '</td>');
            tr.append('<td>' + asset.warehouseLocation + '</td>');

            var delBtn = $('<button type="button" style="border:none;background:#ffffff" class="btn" title="Remove this asset"><i class="bi bi-x-circle"></i></button>').data('ringfenceId', ringfenceId).data('assetId', asset.id);

            delBtn.click(function () {
                pf_removeAsset($(this).data('ringfenceId'), $(this).data('assetId'));
            });
            tr.append('<td></td>').append(delBtn);
        }

        $('#panel_ringfence_item').append(table);
    } else {
        $('#panel_ringfence_none').show();
    }
}

function pf_removeAsset(ringfenceId, assetId) {

    var request = {};
    request.ringfenceId = ringfenceId;
    request.assetId = assetId;

    $('#headerRefresh').show();

    $.ajax({
        type: "DELETE",
        url: "/api/FulfilmentEngine/RemoveRingfenceAsset",
        data: JSON.stringify(request),
        contentType: "application/json",
        success: function (data) {
            $('#headerRefresh').hide();
            if (data.isSuccess) {
                pf_selectRingfence(ringfenceId);
            } else {
                showError(data.errorMessage);
            }
        }
    });
}
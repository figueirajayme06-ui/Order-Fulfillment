var productLinesJSON = [];
function getProductLines() {

    if (productLinesJSON.length == 0) {
        $.ajax({
            url: "/api/Product/ProductLines",
            type: "get",
            async: false,
            success: function (data) {
                productLinesJSON = data;
            }
        });
    }

    return productLinesJSON;
}

var genericsJSON = [];
function getGenerics() {
  
    if (genericsJSON.length == 0) {
        $.ajax({
            url: "/api/Product/Generics?division=" + headerData.division,
            type: "get",
            async: false,
            success: function (data) {
                genericsJSON = data;
            }
        });
    }
    return genericsJSON;
}

var itemNumbersJSON = [];
function getItemNumbers() {

    if (itemNumbersJSON.length == 0) {
        $.ajax({
            url: "/api/Product/ItemNumbers?division=" + headerData.division,
            type: "get",
            async: false,
            success: function (data) {
                itemNumbersJSON = data;
            }
        });
    }
    return itemNumbersJSON;
}

function redirectToConfigurator(button) {
    var value = $("#lstAddNewGeneric").igCombo("value");
    var generic = genericsJSON.find(i => i.id === value);
    window.location.href = $(button).attr('data-url') + '&genericCode=' + generic.code;
}

$("#lstAddItemNumberLine").igCombo({
    width: 510,
    dataSource: productLinesJSON,
    filteringType: 'local',
    textKey: "text",
    valueKey: "id",
    autoComplete: true,
    autoSelectFirstMatch: false,
    selectItemBySpaceKey: false,
    allowCustomValue: false,
    zIndex: 100020,
    visibleItemsCount: 5,
    grouping: {
        key: 'groupText',
        dir: 'asc'
    },
    locale: {
        placeHolder: "Select a product line"
    },
    multiSelection: {
        enabled: false
    },
    dropDownOrientation: "bottom",
    selectionChanged: function (evt, ui) {
        var filteredChildren = [];
        if (ui.items && ui.items[0]) {
            var itemData = ui.items[0].data;

            filteredChildren = genericsJSON.filter(function (item) {
                return item.parentId == itemData.id;
            });
        } else {
            filteredChildren = genericsJSON;
        }

        var $target = $("#lstAddNewGeneric");
        $target.igCombo("deselectAll", {}, true);
        $target.igCombo("option", "dataSource", filteredChildren);
        $target.igCombo("dataBind");

        loadProductAttributes();
    }
});

$("#lstAddNewGeneric").igCombo({
    width: 510,
    dataSource: genericsJSON,
    filteringType: 'local',
    textKey: "text",
    valueKey: "id",
    autoComplete: true,
    autoSelectFirstMatch: false,
    selectItemBySpaceKey: false,
    allowCustomValue: false,
    zIndex: 100020,
    visibleItemsCount: 5,
    locale: {
        placeHolder: "Select a generic"
    },
    multiSelection: {
        enabled: false
    },
    dropDownOrientation: "bottom",
    selectionChanged: function (evt, ui) {
        var filteredChildren = [];
        if (ui.items && ui.items[0]) {
            var itemData = ui.items[0].data;

            filteredChildren = itemNumbersJSON.filter(function (item) {
                return item.parentId == itemData.id;
            });
            $("#btnAddItemOKGeneric").removeAttr("disabled");
        } else {
            filteredChildren = itemNumbersJSON;
            $("#btnAddItemOKGeneric").attr("disabled", "disabled");
        }

        var $target = $("#lstAddNewItemNumber");
        $target.igCombo("deselectAll", {}, true);
        $target.igCombo("option", "dataSource", filteredChildren);
        $target.igCombo("dataBind");

        loadProductAttributes();
    }
});

$("#lstAddNewItemNumber").igCombo({
    width: 510,
    dataSource: itemNumbersJSON,
    filteringType: 'local',
    textKey: "text",
    valueKey: "id",
    autoComplete: true,
    autoSelectFirstMatch: false,
    selectItemBySpaceKey: false,
    allowCustomValue: false,
    zIndex: 100020,
    visibleItemsCount: 5,
    locale: {
        placeHolder: "Select an item number"
    },
    multiSelection: {
        enabled: false
    },
    dropDownOrientation: "bottom",
    selectionChanged: function (evt, ui) {
        if (ui.items && ui.items[0]) {
            $("#btnAddItemOKItem").removeAttr("disabled");
        } else {
            $("#btnAddItemOKItem").attr("disabled", "disabled");
        }
    }
});

$("#pnlAddItem").igDialog({
    headerText: "Product Configurator",
    state: "closed",
    modal: true,
    draggable: false,
    resizable: false,
    height: "500px",
    width: "550px",
    zIndex: 500,
    closeOnEscape: false,
    showCloseButton: false
});

$("#btnAddItemCancel").on("click", function () {
    $("#pnlAddItem").igDialog("close");
});

$("#btnAddItemOKGeneric").on("click", function () {
    $("#pnlAddItem").igDialog("close");
    doAddChildItem(true);
});

$("#btnAddItemOKItem").on("click", function () {
    $("#pnlAddItem").igDialog("close");
    doAddChildItem(false);
});

function doAddChildItem(byGeneric) {

    var request = {};
    request.lineId = referenceLineId;
    request.byGeneric = byGeneric;
    if (byGeneric) {
        request.itemNumber = "";
        request.genericId = $("#lstAddNewGeneric").igCombo("value");
    }
    else {
        request.itemNumber = $("#lstAddNewItemNumber").igCombo("value");
        request.genericId = 0;
    }
    request.attributes = createAttributesString();
    request.quantity = $("#txtAddNewQuantity").val();

    $.ajax({
        type: "PUT",
        url: "/api/FulfilmentEngine/AddLine",
        data: JSON.stringify(request),
        contentType: "application/json",
        success: addChildCallback
    });
}

function addChildCallback(response) {

    var parentClass = 'line_root_' + response.rootAgreementLineNumber;
    var parentRow = $('tr.' + parentClass).last();

    var newRow = parentRow.clone();
    newRow.attr('id', 'line_item_row_' + response.lineId);

    newRow.find('td').eq(0).find('input').eq(0).data('lineid', response.lineId);
    newRow.find('td').eq(1).attr('id', 'line_icon_0_' + response.lineId).removeClass().addClass('line_icon_unfilled  line_icon_' + response.lineId).show();
    newRow.find('td').eq(2).attr('id', 'line_icon_1_' + response.lineId).removeClass().addClass('line_icon_unfilled  line_icon_' + response.lineId).hide();
    newRow.find('td').eq(3).attr('id', 'line_icon_2_' + response.lineId).removeClass().addClass('line_icon_filled line_icon_' + response.lineId).hide();
    newRow.find('td').eq(4).attr('id', 'line_icon_3_' + response.lineId).removeClass().addClass('line_icon_filled line_icon_' + response.lineId).hide();
    newRow.find('td').eq(5).text(response.agreementLineNumber);
    newRow.find('td').eq(6).text(response.itemNumber);
    newRow.find('td').eq(7).text(response.quantity);
    // Reservations blank at this point
    newRow.find('td').eq(8).find('div').empty();
    newRow.find('td').eq(9).find('div').empty();
    newRow.insertAfter(parentRow);
    newRow.find('td').eq(10).find('button').eq(0).data('lineid', response.lineId).click(selectFulfilButton);
    newRow.find('td').eq(10).find('button').eq(1).data('lineid', response.lineId).click(deleteLineButton);

    newRow.removeClass('table-primary');
    newRow.addClass('line_child');
    newRow.addClass("table-success").fadeOut(400, function () {
        newRow.show();
        newRow.removeClass("table-success");
    });

    activateAndOrder(response);
}

var addItemAttribtuesSelected = [];

function createAttributesString() {

    var res = '';

    for (var i = 0; i < addItemAttribtuesSelected.length; i++) {

        if (i > 0) {
            res = res + ';';
        }

        res = res + addItemAttribtuesSelected[i];
    }

    return res;
}

function loadProductAttributes() {

    $('#pnlAddNewAttributes').empty();
    addItemAttribtuesSelected = [];

    var genericId = $("#lstAddNewGeneric").igCombo("value");

    if (genericId > 0) {
        $.ajax({
            url: "/api/Product/Attributes?genericId=" + genericId,
            type: "get",
            async: false,
            success: function (data) {
                processProductAttributes(data);
            }
        });
    }
}

function processProductAttributes(data) {

    var panel = $('#pnlAddNewAttributes');
    var section = null;
    var sectionPanel = null;
    var itemCount = 0;

    for (var i = 0; i < data.length; i++) {
        if (section == null || section != data[i].name) {

            if (section != null && itemCount <= 1) {
                sectionHeader.hide();
                sectionPanel.hide(); // Remove empty sections or those with only one possible value
            }
            section = data[i].name;
            sectionHeader = $('<div class="alert alert-primary mt-2" role="alert"></div>').text(section);
            sectionPanel = $('<ul class="list-group mt-2"/>');
            panel.append(sectionHeader);
            panel.append(sectionPanel);
            itemCount = 0;
        }
        var attr = data[i];
        var attrName = attr.name;
        var attrValues = attr.value.split(';');
        var usedValues = [];

        for (var v = 0; v < attrValues.length; v++) {
            if (attrValues[v].length == 0) {
                continue;
            }

            if (usedValues.indexOf(attrValues[v]) > -1) {
                continue;
            }

            usedValues.push(attrValues[v]);
        }

        usedValues.sort();

        for (var v = 0; v < usedValues.length; v++) {
            var newItem = $('<li class="list-group-item">');
            var newCheck = $('<input type="checkbox" class="form-check-input me-1" value="" aria=label="...">').data('attribute', attrName).data('attributevalue', usedValues[v]);
            newItem.append(newCheck);
            newItem.append($('<span>').text(usedValues[v]));
            sectionPanel.append(newItem);
            itemCount = itemCount+1;

            newCheck.change(function () {

                // Process
                var attrName = $(this).data('attribute');
                var attrValue = $(this).data('attributevalue');
                if ($(this).prop('checked')) {
                    addItemAttribtuesSelected.push(attrName + ":" + attrValue);
                } else {
                    var index = addItemAttribtuesSelected.indexOf(attrName + ":" + attrValue);
                    if (index > -1) {
                        addItemAttribtuesSelected.splice(index, 1);
                    }
                }

                filterByAttributes();
            });
        }
    }
}

function filterByAttributes() {

    var division = headerData.division;
    var genericId = $("#lstAddNewGeneric").igCombo("value");
    var attributes = createAttributesString();

    $.ajax({
        url: "/api/Product/ItemNumbersAttributes?genericId=" + genericId + "&division=" + division + "&attributes=" + encodeURI(attributes),
        type: "get",
        async: false,
        success: function (data) {
            processFilterByAttributes(data);
        }
    });
}

function processFilterByAttributes(data) {

    var $target = $("#lstAddNewItemNumber");
    $target.igCombo("deselectAll", {}, true);
    $target.igCombo("option", "dataSource", data);
    $target.igCombo("dataBind");
}

$("#btnAddLine").on("click", function () {
    getProductLines();
    getGenerics();
    getItemNumbers();

    $("#txtAddNewQuantity").val(1);

    $("#lstAddItemNumberLine").igCombo("deselectAll", {}, true);
    $("#lstAddItemNumberLine").igCombo("option", "dataSource", productLinesJSON);
    $("#lstAddItemNumberLine").igCombo("dataBind");
    $("#lstAddItemNumberLine").igCombo("value", headerData.productLineId);

    $("#lstAddNewGeneric").igCombo("deselectAll", {}, true);
    var filteredGenerics = genericsJSON.filter(function (item) {
        return item.parentId == headerData.productLineId;
    });
    $("#lstAddNewGeneric").igCombo("option", "dataSource", filteredGenerics);
    $("#lstAddNewGeneric").igCombo("dataBind");

    $("#lstAddNewItemNumber").igCombo("deselectAll", {}, true);
    $("#lstAddNewItemNumber").igCombo("option", "dataSource", itemNumbersJSON);
    $("#lstAddNewItemNumber").igCombo("dataBind");

    $("#pnlAddItem").igDialog("open");
});
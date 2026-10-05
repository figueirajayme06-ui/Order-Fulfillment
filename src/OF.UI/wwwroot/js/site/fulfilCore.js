const siblingCookie = "evicted-siblings";

$(document).ready(function () {

    sizeCards();
    $('#dialogCloseButton').click(function () {
        window.parent.postMessage("closing " + window.location, "*");
        window.parent.closeFulfilmentDialog();
    });

    $('*[data-info]').click(function () {
        showMessage($(this).attr("data-item") + ": " + $(this).attr("title"), $(this).attr("data-line"));
    });

    $('.toggleHide').hide();

    $('button.line_btn_fulfil').click(selectFulfilButton);
    $('button.line_btn_delete').click(deleteLineButton);

    recalculateHeaderStatus();

    $('#showFulfiledSwitch').change(function () {
        recalculateHeaderStatus();
    });

    $('#btnNotes').click(function () {
        openNotesView($(this).data('notekey').toString(), 'agreement', refreshNotesIndicator);
    });

    refreshNotesIndicator();

    let form = document.getElementById("summaryForm");
    if (form) {
        form.addEventListener("submit", (e) => {
            e.preventDefault();
            window.open(e.target.action, '\_blank')
        });
    }

    let activateForm = document.getElementById("activateForm");
    if (activateForm) {
        activateForm.addEventListener("submit", (e) => {
            e.preventDefault();

            showConfirm('Activate agreement', 'Are you sure you want to activate this agreement?', function () {
                $("#btnActivate").prop("disabled", true);

                fetch(e.target.action, {
                    method: 'POST',
                    headers: {
                        'Accept': 'application/json',
                        'Content-Type': 'application/json'
                    }
                }).then((response) => {
                    response.json().then((content) => {
                        setTimeout(() => {
                            refreshFrame();
                        }, 4000);
                    }).catch((reason) => {
                        setTimeout(() => {
                            refreshFrame();
                        }, 4000);
                    });
                });
            }, function () { });
        });
    }



    $('#btnStop').click(function (event) {
        event.preventDefault();

        showConfirm('Stop processing', 'Are you sure you want to stop activation of all non sub-lines?', function () {
            $("#btnStop").prop("disabled", true);

            const headerId = $('#btnStop').attr("data-header");

            fetch("/api/Order/CancelActivation/" + headerId, {
                method: 'POST',
                headers: {
                    'Accept': 'application/json',
                    'Content-Type': 'application/json'
                }
            }).then((response) => {
                response.json().then((content) => {
                    setTimeout(() => {
                        refreshFrame();
                    }, 2000);
                }).catch((reason) => {
                    setTimeout(() => {
                        refreshFrame();
                    }, 2000);
                });
            });
        }, function () { });
    });

    

    let abandonForm = document.getElementById("abandonForm");
    if (abandonForm) {
        abandonForm.addEventListener("submit", (e) => {
            e.preventDefault();

            showConfirm('Abandon Quote', 'Are you sure you want to abandon this Quote?', function () {
                $("#btnAbandon").prop("disabled", true);

                fetch(e.target.action, {
                    method: 'DELETE',
                    headers: {
                        'Accept': 'application/json',
                        'Content-Type': 'application/json'
                    }
                }).then((response) => {
                    response.json().then((content) => {
                        setTimeout(() => {
                            refreshFrame();
                        }, 1000);
                    }).catch((reason) => {
                        setTimeout(() => {
                            refreshFrame();
                        }, 1000);
                    });
                });
            }, function () { });

        });
    }

    $("#pnlErrorsDialog").igDialog({
        headerText: "Activation Errors",
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

    $('#btnActivationRequested,#btnActivationError').click(function () {
        $("#pnlErrorsDialog").igDialog("open");
    });
    
    $('#btnErrorCancel').click(function () {
        $("#pnlErrorsDialog").igDialog("close");
    });

    $('#btnActivationStale').click(function () {
        showMessage(
            'This order activation is taking longer than expected to complete. Please contact support.', 'Activation Taking Longer Than Expected.'
        );
    });
});

function sizeCards() {

    var ref = $('#card_lines');
    $('#card_lines').height($(window).height() - ref.position().top - 30);
    $('#table_lines').height($(window).height() - ref.position().top - 170);

    var ref = $('#card_fulfilment');
    $('#card_fulfilment').height($(window).height() - ref.position().top - 30);
    $('#pf_items_div').height($(window).height() - ref.position().top - 285);
};

function refreshNotesIndicator() {
    var notes = $('#btnNotes');

    if (notes.length > 0) {
        getNoteCount($('#btnNotes').data('notekey').toString(), 'agreement', function (data) {
            $('#btnNotes').find('span').text(data);
        });
    }
}

function deleteLineButton() {
    var id = $(this).data('lineid');
    showConfirm('Delete Line', 'Are you sure you want to delete this line?', function () {
        var request = {};
        request.lineId = id;
        callDeleteLine(request, deleteLineCallback);
    },function () { });
}


function callDeleteLine(request, callback) {
    $.ajax({
        type: "DELETE",
        url: "/api/FulfilmentEngine/DeleteLine",
        data: JSON.stringify(request),
        contentType: "application/json",
        success: callback
    });
}

function deleteLineCallback(response) {

    if (response.isSuccess) {
        $("#line_item_row_" + response.lineId).toggleClass("table-danger").fadeOut(400, function () {
            $(this).remove();
            selectedWarehouse = null;
            $('#panel_fulfilment_blank').show();
            $('#panel_fulfilment_error').hide();
            $('#panel_fulfilment_item').hide();
            recalculateHeaderStatus();
            activateAndOrder(response);
        });

    } else {
        showError("Could not delete line: " + response.errorMessage);
    }
}

var referenceLineId = 0; // For add item

function selectFulfilButton() {
    var id = $(this).data('lineid');
    selectFulfil(id);
}
function refreshFulfilLine(id) {
    selectFulfil(id, 'table-warning');
}
function selectFulfil(id, additionalClass) {

    referenceLineId = id;
    selectedWarehouse = null;
    $('#panel_fulfilment_blank').hide();
    $('#panel_fulfilment_error').hide();
    $('#panel_fulfilment_item').show();

    var request = {};
    request.lineId = id;

    $('.line_item_row').removeClass('table-primary');
    $('.line_item_row').removeClass('table-warning');
    $('#line_item_row_' + id).addClass('table-primary');

    if (additionalClass) {
        $('#line_item_row_' + id).addClass(additionalClass);
    }

    if (!warehouses || warehouses.length == 0) {
        $.ajax({
            type: "GET",
            url: "/api/FulfilmentEngine/GetWarehouses",
            contentType: "application/json",
            success: function (response) {
                if (!response.isSuccess) {
                    return;
                }
                warehouses = response.warehouses;
            }
        });
    }

    callFulfilment(request, drawFulfilment);
}

var headerData = null;
var checkOptions = [];

function drawFulfilment(response) {
    if (response.isSuccess) {
        headerData = response;
        checkOptions = [];

        if (response.isChild) {
            $('#btnAddLine').hide();
        } else {
            $('#btnAddLine').show();
        }

        var alternatives = $('#lstAlternatives');
        alternatives.empty();

        alternatives.append($('<li><strong>Depot Options</strong></li >'));
        var depotFulfillOption = $('<a id="depotFulfillOption" class="dropdown-item" href="javascript:depotFulfill()">Depot Fulfil</a>')
            .data('warehouse', headerData.warehouse)
            .data('quantity', 1)
        alternatives.append($(depotFulfillOption).wrap('<li></li>').parent());

        if (response.alternativeOptions.length > 0) {
            alternatives.append($('<li><strong>Rehire Options</strong></li >'));
            for (var a = 0; a < response.alternativeOptions.length; a++) {
                var alt = response.alternativeOptions[a];
                alternatives.append($('<li><a class="dropdown-item" href=\'javascript:rehireFulfil("' + alt.itemNumber + '")\'">' + alt.description + '</a></li>'));
            }
        }

        $('#pf_title').text(response.genericCode + ": " + response.itemDescription);
        $('#pf_attributes').empty();
        $('#pf_results').empty();
        $('#pf_spinner').show();
        if (response.attributes.length > 0) {
            for (var i = 0; i < response.attributes.length; i++) {
                var attr = response.attributes[i];
                var displayAttr = (response.displayAttributes && i < response.displayAttributes.length) ? response.displayAttributes[i] : attr;
                var newCheck = $('<input type="checkbox" data-attribute="' + attr + '" class="btn-check" id="btn-check-att-' + i + '" autocomplete="off" checked>');
                var newLabel = $('<label class="btn btn-outline-primary mt-1" for="btn-check-att-' + i + '">' + displayAttr + '&nbsp;<i class="bi bi-x-circle"></i></label>');
                var newSpace = $('<span>&nbsp;</span>');
                $('#pf_attributes').append(newCheck);
                $('#pf_attributes').append(newLabel);
                $('#pf_attributes').append(newSpace);

                checkOptions.push(newCheck);
                newCheck.change(function () {
                    $('#pf_results').empty();
                    $('#pf_spinner').show();
                    refreshStock();
                });
            }
        }

        refreshStock();
    }
    else {
        $('#panel_fulfilment_error').show();
        $('#panel_fulfilment_item').hide();
        $('#panel_error_message').text(response.errorMessage);
    }
}

function refreshStock() {

    var request = {};
    request.lineId = headerData.lineId;
    request.isSerialized = headerData.isSerialized;
    request.warehouse = headerData.warehouse;

    var attributes = [];
    for (var c = 0; c < checkOptions.length; c++) {
        var option = checkOptions[c];
        if (option.is(":checked")) {
            attributes.push(option.data('attribute'));
        }
    }

    request.attributes = attributes;

    callStock(request, refreshFulfillmentCard);
}

function refreshFulfillmentCard(response) {
    $('#depotFulfillOption').data('quantity', response.isSuccess ? response.quantity : 1)
    drawStock(response)
}

function refreshFrame() {
    window.location.reload();
}

var warehouses = [];

function drawStock(response) {
    $('#pf_spinner').hide();
    $('#pf_results').empty();

    if (response.isSuccess) {

        if (response.isSerialized) {

            drawBars(response, response.serializedItems, $('#pf_results'), true);

        } else {

            drawBars(response, response.nonSerializedItems, $('#pf_results'), false);
        }

    } else {

        $('#pf_results').append($("<div class='fulfilResults'><i class='bi bi-sign-stop'></i>&nbsp;" + response.errorMessage + "</div>"));
    }
}

function getReservationsForLine(message, items, isSerialized) {

    var result = [];
    for (var i = 0; i < items.length; i++) {

        for (var r = 0; r < items[i].reservations.length; r++) {

            if (items[i].reservations[r].lineId == message.lineId) {
                var reservation = items[i].reservations[r];

                reservation.warehouse = items[i].asset.warehouse;
                reservation.assetStatus = items[i].asset.status;
                if (isSerialized) {

                    reservation.assetId = items[i].asset.id;
                    reservation.label = items[i].asset.warehouse + ': ' + items[i].asset.id;
                    reservation.quantity = 1;

                } else {

                    reservation.itemNumber = items[i].asset.itemNumber;
                    reservation.label = items[i].asset.warehouse + ': ' + items[i].asset.itemNumber;

                }
                reservation.label = reservation.label + '<span class="position-absolute top-0 start-100 translate-middle badge rounded-pill bg-danger">' + reservation.quantity + '</span>';
                result.push(reservation);
            }
        }
    }

    for (var r = 0; r < message.otherReservations.length; r++) {
        var reservation = message.otherReservations[r];

        reservation.warehouse = reservation.warehouse;
        reservation.reservationId = reservation.id;
        if (reservation.isRehire) {
            reservation.assetId = reservation.itemNumber;
            reservation.label = reservation.warehouse + ': ' + reservation.itemNumber + ' REHIRE';
            reservation.itemNumber = reservation.itemNumber;
        } else if (reservation.isDepotFulfilled) {
            reservation.assetId = 'DEPOTFULFIL';
            reservation.label = reservation.warehouse + ': ' + 'Depot Fulfil';
            reservation.itemNumber = 'DEPOTFULFIL';
        } else {
            // Confirmed on-hire item
            reservation.label = reservation.warehouse + ': ' + reservation.assetId;
        }
        reservation.label = reservation.label + '<span class="position-absolute top-0 start-100 translate-middle badge rounded-pill bg-danger">' + reservation.quantity + '</span>';
        result.push(reservation);
    }

    return result;
}

function setLineStatus(lineId, status, isSerialized, reservations) {

    $('.line_icon_' + lineId).hide();
    $('#line_icon_' + status + '_' + lineId).show();

    var items = [];
    var warehouseItems = [];
    for (var r = 0; r < reservations.length; r++) {
        var reservation = reservations[r];
        if (isSerialized) {
            if (items.indexOf(reservation.assetId) == -1) {
                items.push(reservation.assetId);
            }
            if (warehouseItems.indexOf(reservation.warehouse) == -1) {
                warehouseItems.push(reservation.warehouse);
            }
        } else {
            if (items.indexOf(reservation.itemNumber) == -1) {
                items.push(reservation.itemNumber);
            }
            if (warehouseItems.indexOf(reservation.warehouse) == -1) {
                warehouseItems.push(reservation.warehouse);
            }
        }
    }
    reserveText = items.join(',');
    warehouseReserveText = warehouseItems.join(',');
    $('tr#line_item_row_' + lineId).find('div.reserve-text').attr('title', reserveText).text(reserveText);
    $('tr#line_item_row_' + lineId).find('div.warehouse-reserve-text').attr('title', warehouseReserveText).text(warehouseReserveText);
    
    recalculateHeaderStatus();
}

function drawBars(message, items, container, isSerialized) {
    var reservations = getReservationsForLine(message, items, isSerialized);

    var cart = $('<div style="height:100px" class="card"><h5 id="cart-header-bar" class="card-header"><i class="bi bi-basket-fill"></i>&nbsp;Selected Items</h5><div id="cart-items" style="overflow-y:scroll" class="card-body"></div></div>');
    container.append(cart);
    var cartItems = $('#cart-items');

    for (var r = 0; r < reservations.length; r++) {

        var reservation = reservations[r];
        var deletedStatuses = ['RemovedStock', 'Scrap', 'Sold'];
        var isDeletedReservation = reservation.assetStatus && deletedStatuses.indexOf(reservation.assetStatus) >= 0;
        var btnClass = isDeletedReservation ? 'btn btn-outline-danger position-relative mt-1' : 'btn btn-outline-secondary position-relative mt-1';
        var deletedBadge = isDeletedReservation ? '&nbsp;<span class="badge bg-warning text-dark">' + reservation.assetStatus + '</span>' : '';

        var newCheck = $('<input type="checkbox" class="btn-check mt-1" autocomplete="off" checked>').data('reservationid', reservation.reservationId).attr('id', 'btn-check-reservation-' + reservation.reservationId);
        var newLabel = $('<label class="' + btnClass + '">' + reservation.label + deletedBadge + '&nbsp;<i class="bi bi-x-circle"></i></label>').attr('for', 'btn-check-reservation-' + reservation.reservationId);
        var newSpace = $('<span style="padding-right:10px">&nbsp;</span>');
        cartItems.append(newCheck);
        cartItems.append(newLabel);
        cartItems.append(newSpace);

        newCheck.change(function () {
            deleteReservationForLine($(this).data("reservationid"));
        });
    }

    var cartHeader = $('#cart-header-bar'); 

    if (message.fulfilmentStatus == 3) {
        cartHeader.addClass('bg-success');
        cartHeader.addClass('text-white');
        cartHeader.html('<i class="bi bi-check-circle"></i>&nbsp;Selected Items - line is fully fulfiled')
    }

    if (message.fulfilmentStatus == 1) {
        cartHeader.addClass('bg-warning');
        cartHeader.addClass('text-dark');
        cartHeader.html('<i class="bi bi-circle-half"></i>&nbsp;Selected Items - line is partially fulfiled')
    }

    if (message.fulfilmentStatus == 2) {
        cartHeader.addClass('bg-danger');
        cartHeader.addClass('text-white');
        cartHeader.html('<i class="bi bi-exclamation-diamond"></i>&nbsp;Selected Items - line is over fulfiled')
    }

    let siblingEvictions = JSON.parse(Cookies.get(siblingCookie) || "[]");

    if (message.fulfilmentStatus == 0 && siblingEvictions.includes(message.lineId)) {
        cartHeader.addClass('bg-warning');
        cartHeader.addClass('text-white');
        cartHeader.html('<i class="bi bi-exclamation-diamond"></i>&nbsp;Reservation clash - Fulfilment removed by new reservation')
    }
    

    if (items.length == 0) {

        container.append($("<div class='mt-5 alert alert-danger' role='alert'><i class='bi bi-sign-stop'></i>&nbsp;No stock available</div>"));

    } else {
        var depotList = $('<select id="lstDepots" style="margin-top:5px;margin-bottom:5px" class="form-select" aria-label="Depots"></select>');

        depotList.change(function () {
            var selected = $(this).val();
            $('#depotFulfillOption').data('warehouse', selected)
            selectWarehouse(selected);
        });

        container.append(depotList);

        var itemsdiv = $('<div id="pf_items_div" class="barItemsContainer"></div>');
        var table = $('<table class="table table-fixed"></table>');
        var tbody = $('<tbody></tbody>');
        table.append(tbody);

        var warehouse = null;
        var firstColCount = isSerialized ? 6 : 4;
        var ganntCols = message.cols;
        var headerDrawn = false;

        items = items.filter(item => item.asset && item.asset.itemNumber);

        for (var i = 0; i < items.length; i++) {

            var item = items[i];

            if (warehouse == null || warehouse != item.asset.warehouse) {
                warehouse = item.asset.warehouse;

                var depotOption = $('<option></option>').text(warehouse + ': ' + item.warehouseName).attr("value", warehouse);
                if (warehouse == selectedWarehouse || selectedWarehouse == null || selectedWarehouse == '') {
                    selectedWarehouse = warehouse;
                    depotOption.attr("selected", "selected");
                };
                depotList.append(depotOption);
            }


            if (item.asset.warehouse == selectedWarehouse) {

                if (!headerDrawn) {
                    var headerrow = $('<tr></tr>').addClass("wh-itemrow").addClass("wh-" + warehouse).append($('<th class="table-secondary" style="text-align:left"></th>').attr('colspan', ganntCols + firstColCount).text(warehouse + ': ' + item.warehouseName));
                    tbody.append(headerrow);
                    var tr = $('<tr></tr>').addClass("wh-itemrow").addClass("wh-" + warehouse);
                    tr.append($('<th style="width:15px"></th>').text(""));
                    if (isSerialized) tr.append($('<th style="width:100px"></th>').text("Asset"));
                    tr.append($('<th style="width:100px"></th>').text("Item"));
                    tr.append($('<th style="width:200px"></th>').text("Description"));
                    if (isSerialized) tr.append($('<th style="width:40px"></th>').text("Location"));
                    tr.append($('<th style="width:15px"></th>').text(""));
                    tr.append($('<th class="table-warning" style="width:auto"></th>').attr('colspan', ganntCols).append($("<span>").text(message.startDateString)).append($("<span style='float:right'>").text(message.endDateString)));
                    tbody.append(tr);
                    headerDrawn = true;
                }

                var tr = $('<tr></tr>').addClass("wh-itemrow").addClass("wh-" + warehouse);

                var checkbox = $('<input class="form-check-input" type="checkbox" value="">').data('warehouse', item.asset.warehouse).data('itemid', item.asset.itemNumber).data('multiple', item.substitutionMultiple);

                if (isSerialized) {
                    checkbox.data('assetid', item.asset.id);
                } else {
                    checkbox.data('assetid', item.asset.itemNumber);
                }

                // Check if already reserved
                var reservationId = 0;
                for (var r = 0; r < reservations.length; r++) {
                    if (isSerialized && reservations[r].assetId == item.asset.id) {
                        reservationId = reservations[r].reservationId;
                        break;
                    }

                    if (!isSerialized && reservations[r].itemNumber == item.asset.itemNumber && reservations[r].warehouse == item.asset.warehouse) {
                        reservationId = reservations[r].reservationId;
                        break;
                    }
                }

                if (reservationId > 0) {
                    checkbox.prop('checked', true);
                    checkbox.data('reservationid', reservationId);
                }

                var deletedStatuses = ['RemovedStock', 'Scrap', 'Sold'];
                var isDeletedAsset = item.asset.status && deletedStatuses.indexOf(item.asset.status) >= 0;

                if (isDeletedAsset && reservationId == 0) {
                    checkbox.prop('disabled', true);
                    checkbox.attr('title', 'This asset has been ' + item.asset.status + ' and cannot be allocated.');
                }
                
                var lineQuantity = message.quantity
                checkbox.change(function () {
                    var chk = $(this);
                    if (chk.is(":checked")) {

                        reserveItemForLine(
                            chk.data("warehouse"),
                            chk.data("itemid"),
                            chk.data("assetid"),
                            chk.data("minavailableinperiod"),
                            false,
                            false,
                            chk.data("multiple"),
                            undefined,
                            lineQuantity
                        );

                    } else {

                        deleteReservationForLine(chk.data("reservationid"));
                    }
                });

                tr.append($('<td></td>').append($(checkbox)));

                if (isSerialized) {
                    // Asset ID - serialized only
                    var assetIdTD = $('<td></td>').addClass(reservationId > 0 ? 'table-secondary nowrap' : 'table-primary nowrap');
                    assetIdTD.data('assetid', item.asset.id);

                    if (isDeletedAsset) {
                        assetIdTD.html('<i class="bi bi-exclamation-triangle-fill text-warning"></i>&nbsp;<s>' + item.asset.id + '</s>&nbsp;<span class="badge bg-danger">' + item.asset.status + '</span>');
                        assetIdTD.attr('title', 'This asset has been ' + item.asset.status + ' and is no longer available.');
                        assetIdTD.css('opacity', '0.7');
                    }
                    else if (item.noteCount > 0) {
                        assetIdTD.html('<i class="bi bi-exclamation-square"></i>&nbsp;' + item.asset.id);
                        assetIdTD.attr('title', 'This asset has one or more notes. Click here to view.');
                        assetIdTD.addClass('text-danger');
                    }
                    else {
                        assetIdTD.html('<i class="bi bi-pencil-square"></i>&nbsp;' + item.asset.id);
                        assetIdTD.attr('title', 'Click here to add notes to this asset.');
                    }
                    assetIdTD.click(function () {
                        openNotesView($(this).data('assetid'), 'asset', refreshStock);
                    });

                    tr.append(assetIdTD);
                }

                var itemTD = $('<td class="nowrap"></td>').text(item.asset.itemNumber);
                tr.append(itemTD);
                var descripionTD = $('<td class="nowrap"></td>').text(item.asset.description);
                tr.append(descripionTD);

                if (isSerialized) {
                    const whlo = $('<td class="nowrap"></td>').text(item.asset.warehouseLocation);
                    whlo.attr('title', 'Warehouse Location: ' + item.asset.warehouseLocation);
                    tr.append(whlo);
                }

                var subTD = $('<td class="nowrap"></td>');
                switch (item.substitutionReason) {
                    case "UP":
                        subTD.attr('title', 'Substitute up').append($('<i class="bi bi-arrow-up-circle"></i>'));
                        break;

                    case "MULTIPLE":
                        subTD.attr('title', 'Substitute multiple (*' + item.substitutionMultiple + ')').append($('<i class="bi bi-bounding-box"></i>'));
                        break;

                    case "RELATED":
                        subTD.attr('title', 'Related substitute').append($('<i class="bi bi-arrow-left-right"></i>'));
                        break;

                }
                tr.append(subTD);

                var bars = message.bars[i];
                var minAvailableInPeriod = message.quantity;

                for (var g = 0; g < ganntCols; g++) {
                    var barTD = $('<td class="barCell"></td>');
                    for (var b = 0; b < bars.length; b++) {
                        var bar = bars[b];
                        if (bar.quantity < minAvailableInPeriod || b == 0) {
                            minAvailableInPeriod = bar.quantity;
                        };
                        if (g <= bar.end && g >= bar.start) {
                            var length = bar.end - bar.start + 1;
                            if (g + length > ganntCols) {
                                length = ganntCols - g;
                            }
                            barTD = $('<td colspan="' + length + '" class="nowrap ' + bar.cssClass + '" style="text-align:center;width:auto;border:dashed 1px #fafafa;" title="' + bar.text + '">' + (isSerialized ? bar.text : bar.shortText) + '</td>');
                            g = g + length - 1;
                            break;
                        }
                    }
                    tr.append(barTD);
                }

                checkbox.data('minavailableinperiod', minAvailableInPeriod);

                if (minAvailableInPeriod == 0) {
                    itemTD.addClass("table-danger ");
                    descripionTD.addClass("table-danger ");
                } else {
                    itemTD.addClass("table-light ");
                    descripionTD.addClass("table-light ");
                }

                // Grey out item and description for deleted assets
                if (isDeletedAsset) {
                    itemTD.css('opacity', '0.7');
                    descripionTD.css('opacity', '0.7');
                    if (!isSerialized) {
                        itemTD.html('<s>' + item.asset.itemNumber + '</s>&nbsp;<span class="badge bg-danger">' + item.asset.status + '</span>');
                    }
                }

                tbody.append(tr);
            }
        }

        itemsdiv.append(table);
        container.append(itemsdiv);
    }
    setLineStatus(message.lineId, message.fulfilmentStatus, isSerialized, reservations);
    sizeCards();
    restoreBarScroll();
}

var selectedWarehouse = "";
function selectWarehouse(warehouse) {
    selectedWarehouse = warehouse;
    drawStock(cachedStockResponse);
}

function callFulfilment(request, callback) {
    $.ajax({
        type: "POST",
        url: "/api/FulfilmentEngine/Satisfy",
        data: JSON.stringify(request),
        contentType: "application/json",
        success: callback
    });
}

var cachedStockResponse = null;

function callStock(request, callback) {
    $.ajax({
        type: "POST",
        url: "/api/FulfilmentEngine/Stock",
        data: JSON.stringify(request),
        contentType: "application/json",
        success: function (response) {
            cachedStockResponse = response; // Cache the response for later use
            callback(response);
        }
    });
}

function chkAll() {
    $('input.chk-all').prop('checked', $('#chkTopAll').prop('checked'));
}

function chkPackage(id) {
    $('input.chk-package-' + id).prop('checked', $('#chkTopPackage-' + id).prop('checked'));
}

function refreshDataCallback() {
    saveBarScroll();
    refreshStock();
}

function depotFulfill() {
    var depotFulfillOption = $('#depotFulfillOption')
    var warehouse = depotFulfillOption.data('warehouse')
    var quantity = depotFulfillOption.data('quantity')
    reserveItemForLine(warehouse, 'DEPOTFULFIL', 'DEPOTFULFIL', 0, false, true, undefined, undefined, quantity);
}

function rehireFulfil(itemNumber) {
    reserveItemForLine(headerData.warehouse, itemNumber, itemNumber, 0, true, false);
}

function reserveItemForLine(warehouse, itemNumber, assetId, minAvailableInPeriod, isRehire, isDepotFulfiled, multiple, isCallback, quantity) {
    // if this is a depotfulfil or a non-serialized item, plus isn't a callback

    if ((!headerData.isSerialized || isDepotFulfiled) && !isCallback) {
        showModalForReservation(warehouse, itemNumber, assetId, minAvailableInPeriod, isRehire, isDepotFulfiled, multiple, isCallback, quantity, headerData.isSerialized);
    } else {

        var requested = 1;

        if (isCallback) {
            requested = quantity;
        }

        if (minAvailableInPeriod < requested && !isRehire && !isDepotFulfiled) {
            const collidingReservations = getCachedStockReservationsCollisionsForItem(itemNumber, assetId);
            let message = 'There is no stock available for this item. ';

            if (collidingReservations.length > 0) {
                message += collidingReservations.length > 1 ? 'Conflicting reservations on lines: ' : 'Conflicting reservation on line: ';
                message += collidingReservations.map(r => r.agreementLineNumber ? `${r.agreementLineNumber} (${r.lineId})` : `(${r.lineId})`).join(', ');
                message += '.<br>';
            }

            message += 'Do you want to reserve anyway?';

            showConfirm('Insufficient stock',
                message,
                function () {
                    continueReserveItemForLine(warehouse, itemNumber, assetId, minAvailableInPeriod, isRehire, isDepotFulfiled, multiple, requested);
                },
                function () {
                    refreshDataCallback();
                }
            );
        } else {
            continueReserveItemForLine(warehouse, itemNumber, assetId, minAvailableInPeriod, isRehire, isDepotFulfiled, multiple, requested);
        }
    }
}

function getCachedStockItems() {
    if (cachedStockResponse && cachedStockResponse.isSuccess) {
        return cachedStockResponse.isSerialized ? cachedStockResponse.serializedItems : cachedStockResponse.nonSerializedItems;
    }

    console.warn('Stocks were not successfully fetched');
    return [];
}

function getCachedStockReservationsCollisionsForItem(itemNumber, assetId) {
    if (!itemNumber || !assetId) {
        console.warn('item number and asset id must be provided');
        return [];
    }

    const startDate = new Date(cachedStockResponse.startDate).getTime();
    const endDate = new Date(cachedStockResponse.endDate).getTime();
    const items = getCachedStockItems();
    const result = items
        .filter(item => item.asset.id == assetId && item.asset.itemNumber == itemNumber)
        .flatMap(item => item.reservations.filter(r =>
            new Date(r.deliveryDate ?? r.validFromDate).getTime() <= endDate &&
            startDate <= new Date(r.terminationDate ?? r.validToDate).getTime()));
    return result;
}

function showModalForReservation(warehouse, itemNumber, assetId, minAvailableInPeriod, isRehire, isDepotFulfiled, multiple, isCallback, quantity, isSerialized) {
    let warehouseDropDown = `
        <div class="form-group pb-2">
            <label for="warehouse">Please select a warehouse to fulfil from:</label>
            <select id="warehouse" type="text" value="" class= "warehouse form-control form-control-lg" required>
                ${warehouses.map(w => {
                    if (w.warehouseCode == warehouse) {
                        return `<option value="${w.warehouseCode}" selected>${w.warehouseCode}: ${w.warehouse}</option>`
                    } else {
                        return `<option value="${w.warehouseCode}">${w.warehouseCode}: ${w.warehouse}</option>`
                    }
                }).join('')}
            </select>
        </div>
        `;
    let quantityDropDown = `
        <div class="form-group">
            <label for="quantity">Please enter the number of items to reserve:</label>
            <input id="quantity" type="text" value="${isPositiveInteger(quantity) ? Number(quantity) : 1}" class= "quantity form-control form-control-lg" required />
        </div>
        `;
    let quantityHidden = `
        <input id="quantity" type="hidden" value="1" class="quantity" />
        `;
    let modalForm = `
        <form action="">
            ${isDepotFulfiled ? warehouseDropDown : ''}
            ${isSerialized ? quantityHidden : quantityDropDown}
        </form>`;

    // Ask for options (quantity, warehouse)
    $.confirm({
        title: 'Fulfilment Options',
        content: modalForm,
        buttons: {
            formSubmit: {
                text: 'OK',
                btnClass: 'btn-blue',
                action: function () {
                    var modelQuantity = this.$content.find('.quantity').val();
                    if (!modelQuantity || !isPositiveInteger(modelQuantity)) {
                        $.alert('Error', 'Please provide a valid quantity');
                        return false;
                    }
                    quantity = modelQuantity;

                    // If this is a depotfulfil it should also ask for a warehouse to be selected
                    if (isDepotFulfiled) {
                        var modelWarehouse = this.$content.find('.warehouse').val();
                        if (!modelWarehouse || modelWarehouse.length != 3) {
                            $.alert('Error', `Warehouse ${modelWarehouse} is invalid, it's length is ${modelWarehouse.length}.`);
                            return false;
                        }
                        warehouse = modelWarehouse;
                    }
                    reserveItemForLine(warehouse, itemNumber, assetId, minAvailableInPeriod, isRehire, isDepotFulfiled, multiple, true, parseInt(quantity));
                }
            },
            cancel: function () {
                //close
                refreshDataCallback();
            },
        },
        onContentReady: function () {
            // bind to events
            var jc = this;
            this.$content.find('form').on('submit', function (e) {
                // if the user submits the form by pressing enter in the field.
                e.preventDefault();
                jc.$$formSubmit.trigger('click'); // reference the button and click it
            });
        }
    });
}

function continueReserveItemForLine(warehouse, itemNumber, assetId, minAvailableInPeriod, isRehire, isDepotFulfiled, multiple, quantity) {

    var request = {};
    request.isDelete = false;
    request.reservationId = 0;
    request.lineId = headerData.lineId;
    request.warehouse = warehouse;
    request.itemNumber = itemNumber;
    request.assetId = assetId;
    request.isSerialized = headerData.isSerialized;
    request.minAvailableInPeriod = minAvailableInPeriod;
    request.isRehire = isRehire;
    request.isDepotFulfiled = isDepotFulfiled;
    request.multiple = 1;
    if (multiple > 0) {
        request.multiple = multiple;
    }
    request.quantity = quantity;
    callReserve(request, reserveCallback);
}

function deleteReservationForLine(reservationId) {

    var request = {};
    request.isDelete = true;
    request.reservationId = reservationId;
    request.lineId = 0;
    request.warehouse = "";
    request.itemNumber = "";
    request.assetId = "";
    request.isSerialized = headerData.isSerialized;
    request.minAvailableInPeriod = 0;
    request.isRehire = false;
    request.isDepotFulfiled = false;
    request.multiple = 1;
    callReserve(request, reserveCallback);
}

var barSavedState = false;
var barScrollTop = 0;
var barScrollLeft = 0;

function saveBarScroll() {

    barScrollTop = $('#pf_items_div').scrollTop();
    barScrollLeft = $('#pf_items_div').scrollLeft();
    barSavedState = true;
}

function restoreBarScroll() {

    if (barSavedState) {
        $('#pf_items_div').scrollTop(barScrollTop);
        $('#pf_items_div').scrollLeft(barScrollLeft);
    }
    barSavedState = false;
}

function callReserve(request, callback) {

    $('#pf_spinner').show();
    saveBarScroll();
    $.ajax({
        type: "POST",
        url: "/api/FulfilmentEngine/Reserve",
        data: JSON.stringify(request),
        contentType: "application/json",
        success: callback
    });
}

function activateAndOrder(response) {
    if (response.isSuccess && response.headerStatus >= 0) {

        let isPartiallyFulfilled = parseInt(response.headerStatus) >= 1;
        let isFulfilled = parseInt(response.headerStatus) >= 2;
        let isValidforSummary = $("#summaryForm").attr('data-valid-for-summary') == "true";
        let isActivatable = $("#btnActivate").attr('data-can-activate') == "true" || response.isActivatable == true;
        let hasFulfilledLines = $('td.line_icon_filled:visible, td.line_icon_partial:visible, td.line_icon_overfilled:visible').length > 0;

        let canOrderSummary = isPartiallyFulfilled && isValidforSummary;
        $("#summaryButton").prop('disabled', !canOrderSummary);

        let activationEnabled = shouldEnableActivation(isFulfilled, hasFulfilledLines, isActivatable);
        $("#btnActivate").prop('disabled', !activationEnabled);
    }
}

function reserveCallback(response) {

    $('#pf_spinner').hide();
    if (response.isSuccess) {

        refreshStock();

        if (response.siblingVictims) {
            Cookies.set(siblingCookie, JSON.stringify(response.siblingVictims));

            for (const siblingId of response.siblingVictims) {
                refreshFulfilLine(siblingId);
            }
        }

        activateAndOrder(response);

    } else {

        showError("Could not reserve: " + response.errorMessage);
        refreshStock();
    }
}

function getCheckedItems() {
    var res = [];
    var checkBoxes = $('input.chk-all:checked');
    for (var c = 0; c < checkBoxes.length; c++) {
        if ($(checkBoxes[c]).data('lineid')) {
            res.push($(checkBoxes[c]).data('lineid'));
        }
    }

    return res;
}

function depotFulfilBulk(headerId, warehouse, all) {

    var items = getCheckedItems();
    if (items.length == 0) {
        showMessage('Please check at least one line to action', 'Error');
        return;
    }
    var request = {};
    request.headerId = headerId;
    request.all = all;
    request.isDepotFulfil = true;
    request.isRehire = false;
    request.warehouse = warehouse;
    request.items = items;
    callBulkAction(request, bulkActionCallback);
}

function rehireBulk(headerId, warehouse, all) {

    var items = getCheckedItems();
    if (items.length == 0) {
        showMessage('Please check at least one line to action', 'Error');
        return;
    }

    var request = {};
    request.headerId = headerId;
    request.all = all;
    request.isDepotFulfil = false;
    request.isRehire = true;
    request.warehouse = warehouse;
    request.items = items;
    callBulkAction(request, bulkActionCallback);
}

function callBulkAction(request, callback) {

    $('#bulk_spinner').show();
    $.ajax({
        type: "POST",
        url: "/api/FulfilmentEngine/BulkAction",
        data: JSON.stringify(request),
        contentType: "application/json",
        success: callback
    });
}

function bulkActionCallback(response) {

    $('#bulk_spinner').hide();
    if (response.isSuccess) {

        document.location = document.location;

    } else {

        showError("Bulk action failed: " + response.errorMessage);
    }
}
function recalculateHeaderStatus() {
    var unfilledLines = $('td.line_icon_unfilled:visible');
    var hasFulfilledLines = $('td.line_icon_filled:visible, td.line_icon_partial:visible, td.line_icon_overfilled:visible').length > 0;
    var hasOnlyOrphanedUnfilledLines = checkForOrphanedQuoteLines() && hasFulfilledLines;

    if (unfilledLines.length > 0 && !hasOnlyOrphanedUnfilledLines) {
        $('#header_icon_unfilled').show();
        $('#header_icon_filled').hide();
    } else {
        $('#header_icon_unfilled').hide();
        $('#header_icon_filled').show();
    }

    $('tr.line_item_row').show();
    if (!$('#showFulfiledSwitch').is(":checked")) {
        $('td.line_icon_filled:visible').each(function () {
            $(this).parent().hide();
        });
    }

    let isFulfilled = hasOnlyOrphanedUnfilledLines || (unfilledLines.length === 0);
    let isActivatable = $("#btnActivate").attr('data-can-activate') == "true";

    activateAndOrder({
        isSuccess: true,
        headerStatus: isFulfilled ? 2 : (hasFulfilledLines ? 1 : 0),
        isActivatable: isActivatable
    });
}

function checkForOrphanedQuoteLines() {
    let visibleUnFulfilledLines = $('td.line_icon_unfilled:visible');
    let hasUnFulfilledQuoteLines = visibleUnFulfilledLines.filter(function () {
        return $(this).closest('tr').hasClass('line_quote');
    });

    if (visibleUnFulfilledLines.length > 0 && hasUnFulfilledQuoteLines.length > 0) {
        return hasUnFulfilledQuoteLines.length === visibleUnFulfilledLines.length;
    }

    return false;
}

function shouldEnableActivation(isFulfilled, hasFulfilledLines, isActivatable) {
    let hasOnlyOrphanedLines = checkForOrphanedQuoteLines();

    if (hasOnlyOrphanedLines) {
        return (isFulfilled || hasFulfilledLines) && isActivatable;
    }

    return isFulfilled && isActivatable;
}
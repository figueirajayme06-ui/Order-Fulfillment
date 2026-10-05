var dateCookieName = 'date_range';
var assetEventData = {};
var assetEventsCurrent = false;

// Cookies for date range
var dateCookie = Cookies.get(dateCookieName);

var ganttStartDate = moment().startOf('month');

if (dateCookie) {

    var year = parseInt(dateCookie.substring(0, 4));
    var month = parseInt(dateCookie.substring(5, 7))-1; // Month is zero based for reasons that only the angels know
    ganttStartDate = moment([year, month]);
}

var ganttEndDate = moment(ganttStartDate).add(2, "months");
var currentHeaderId = null;
var currentChangeId = null;

updateDateRangeLabel();
refreshAssetEvents();

$(document).ready(function () {

    window.addEventListener('message', function (event) {
        if (event?.data?.toString()?.includes("/Fulfilment") || event?.data?.toString()?.includes("/ChangeOrder")) {
            console.log(event);
            const url = new URL(window.location);
            url.searchParams.delete("header");
            url.searchParams.delete("page");
            url.searchParams.delete("changeId");
            history.pushState({}, "", url);
        }
    }, false);

    $(document).on("iggriddatabinding", "#grid", function (evt, ui) {
        refreshAssetEvents();
    });

    $(document).on("iggridrendered", "#grid", function (evt, ui) {
        const urlParams = new URLSearchParams(window.location.search);
        const header = urlParams.get('header');

        if (isAssetView) {
            $("#grid").dblclick(assetGridClick);
            $("#grid_fixed").dblclick(assetGridClick);
        }
        else {
            $("#grid").dblclick(taskGridClick);
            $("#grid_fixed").dblclick(taskGridClick);
            if (window.canChangeOrder) {
                $("#grid").click(taskGridSingleClick);
                $("#grid_fixed").click(taskGridSingleClick);

                const page = urlParams.get('page');
                const change = urlParams.get('changeId');

                if (page == "change") {
                    currentHeaderId = header;
                    currentChangeId = change
                    openChangeView();
                    return;
                }
            }

            if (header) {
                openFulfilmentView(header);
            }
        }
    });

    $(document).on("iggriddatabound", "#grid", function (evt, ui) {
        drawGanttHeader();
        
        var isUpdatingGanttTextPosition = false;
        $("#grid_hscroller, #grid_scrollContainer").off('scroll.gantt').on('scroll.gantt', function() {
            if (!isUpdatingGanttTextPosition) {
                isUpdatingGanttTextPosition = true;
                requestAnimationFrame(function() {
                    updateGanttTextPosition();
                    isUpdatingGanttTextPosition = false;
                });
            }
        });
        setTimeout(updateGanttTextPosition, 100);
    });
    
    function updateGanttTextPosition() {
        var container = $('#grid_displayContainer');
        if (!container.length) return;
        var containerLeft = container.offset().left;
        $('.gantt-primary-cell .gantt-text-display').each(function() {
            var span = $(this);
            var offset = containerLeft - span.parent().offset().left;

            if (offset > 0) {
                span.css('left', (offset + 5) + 'px');
            } else {
                span.css('left', '5px');
            }
        });
    }

    $('#btnDateBack').click(function () {

        updateDateRange(-1);
    });
    $('#btnDateForward').click(function () {

        updateDateRange(1);
    });

    $("#dialogWindow").igDialog({
        height: "95%",
        width: "95%",
        modal: true,
        headerText: "Fulfilment Options",
        showMinimizeButton: false,
        showMaximizeButton: false,
        showPinButton: false,
        showCloseButton: false,
        showHeader: true,
        showFooter: false,
        zIndex: 100005,
        state: "closed"
    });

    $("#dialogWindowChange").igDialog({
        height: "95%",
        width: "95%",
        modal: true,
        headerText: "Change Orders",
        showMinimizeButton: false,
        showMaximizeButton: false,
        showPinButton: false,
        showCloseButton: false,
        showHeader: true,
        showFooter: false,
        zIndex: 100005,
        state: "closed"
    });

    $("#btnRefresh").click(function () {
        refresh();
    });
    $("#btnChangeOrders").click(function () {
        openChangeView();
    });

    // Stop filtering on small strings for specific fields which may be causing performance issues.
    // This is a speculative fix requested by management.
    $(document).on("iggridfilteringdatafiltering", "#grid", function (evt, data) {
        // the column that the user just modified the filter on
        const columnThatTriggeredEvent = data.columnKey;

        // even though the key is `newExpressions`, this is the full list of filters
        const allFilters = data.newExpressions;

        const fieldsToFilter = [isAssetView
            ? { field: "Id", condition: "contains", length: 4 }
            : { field: "AgreementNumber", condition: "contains", length: 4 }];

        const outcomes = fieldsToFilter.map(ff => {
            // find the filter, if it exists
            const filter = allFilters.find(f => f.fieldName === ff.field && f.cond == ff.condition);

            // if:
            // - the filter exists
            // - AND it has an expression (optional chaining in case of unexpected structure)
            // - AND that expression is less than X characters long
            // - AND the event was triggered by editing this column
            // then:
            // - do not perform any filtering this time round
            if (filter?.expr?.length < ff.length && columnThatTriggeredEvent === ff.field) {
                return false;
            }

            // if:
            // - the filter exists
            // - AND it has an expression (optional chaining in case of unexpected structure)
            // - AND that expression is less than X characters long
            // then:
            // - remove the filter from the current run
            // - continue current run as the user has triggered this from a different column
            if (filter?.expr?.length < ff.length) {
                // find the index because we have to mutate the existing object for this to work
                const filterIndex = allFilters.findIndex(f => f === filter);
                allFilters.splice(filterIndex, 1);
            }

            return true;
        });

        // if any of the outcomes are `false` then stop this current run
        if (outcomes.some(o => !o)) {
            return false;
        }
    });
});

function checkParentGridForColumnAndRow(columnKey, rowKey) {
    var col = $("#grid").igGrid("columnByKey", columnKey);
    var row = $("#grid").igGrid("findRecordByKey", rowKey);
    return (col != null) && (row != null);
}
function closeChangeDialog() {
    currentChangeId = null;
    if ($("#dialogWindowChange").length > 0) {
        $("#dialogWindowChange").igDialog("close");
    }
    $('#frameChange').attr('src', 'about:blank');
    refresh();
}

function closeFulfilmentDialog() {

    $.ajax({
        url: "/api/Task/Agreement?id=" + openedHeaderId,
        success: function (data) {
            if (data && $("#grid").length > 0) {
                if (checkParentGridForColumnAndRow("FulfilmentStatus", openedHeaderId)) {
                    $("#grid").igGridUpdating("setCellValue", openedHeaderId, "FulfilmentStatus", data.fulfilmentStatus);
                }

                if (checkParentGridForColumnAndRow("LastUpdatedByName", openedHeaderId)) {
                    $("#grid").igGridUpdating("setCellValue", openedHeaderId, "LastUpdatedByName", data.lastUpdatedByName);
                }

                if (checkParentGridForColumnAndRow("LastUpdatedDate", openedHeaderId)) {
                    $("#grid").igGridUpdating("setCellValue", openedHeaderId, "LastUpdatedDate", formatDateAsLocal(data.lastUpdatedDate));
                }

                if (checkParentGridForColumnAndRow("MinFulfilmentStatus", openedHeaderId)) {
                    $("#grid").igGridUpdating("setCellValue", openedHeaderId, "MinFulfilmentStatus", data.minFulfilmentStatus);
                }

                if (checkParentGridForColumnAndRow("MaxFulfilmentStatus", openedHeaderId)) {
                    $("#grid").igGridUpdating("setCellValue", openedHeaderId, "MaxFulfilmentStatus", data.maxFulfilmentStatus);
                }

                if (checkParentGridForColumnAndRow("BarData", openedHeaderId)) {
                    $("#grid").igGridUpdating("setCellValue", openedHeaderId, "BarData", data.barData);
                }

                if (checkParentGridForColumnAndRow("LineCount", openedHeaderId)) {
                    $("#grid").igGridUpdating("setCellValue", openedHeaderId, "LineCount", data.lineCount);
                }
            }
            if ($("#dialogWindow").length > 0) {
                $("#dialogWindow").igDialog("close");
            }
            $('#frameFulfilment').attr('src', 'about:blank');
            refresh();
        }
    });
}

function updateDateRange(step) {

    ganttStartDate = ganttStartDate.add(step, "month").startOf("month");
    ganttEndDate = moment(ganttStartDate).add(2, "month").startOf("month");
    updateDateRangeLabel();
    Cookies.set(dateCookieName, ganttStartDate.toJSON());
    $("#grid").igGrid("dataBind");
}

var scrollToToday = false;

$(document).on("iggridrendered", "#grid_displayContainer", function (evt, ui) {

    // Scroll to today if the date range was updated
    if (scrollToToday) {
        scrollToToday = false;
        var today = moment().startOf('day');
        var daysFromStart = today.diff(ganttStartDate, 'days');
        var scrollPosition = daysFromStart * cellWidth;
        $("#grid_displayContainer").igScroll("option", {
            scrollLeft: scrollPosition,
        });
    }
});

$("#btnToday").igButton();
$("#btnToday").click(function () {
    var today = moment().startOf('day');
    var startDate = moment(ganttStartDate);
    var endDate = moment(ganttEndDate);

    if (!today.isBetween(startDate, endDate, null, '[]')) {
        // If today is not within the current date range, update the date range to show the future three months from today
        ganttStartDate = today.startOf('month');
        ganttEndDate = moment(ganttStartDate).add(2, 'months').endOf('month');
        updateDateRangeLabel();
        Cookies.set(dateCookieName, ganttStartDate.toJSON());
        $("#grid").igGrid("dataBind");
        scrollToToday = true;
    } else {
        var daysFromStart = today.diff(ganttStartDate, 'days');
        var scrollPosition = daysFromStart * cellWidth;
        $("#grid_displayContainer").igScroll("option", {
            scrollLeft: scrollPosition,
        });
    }
});

function updateDateRangeLabel() {

    $('#gridDateRange').html('<i class="bi bi-clock-history"></i>&nbsp;' + ganttStartDate.format('MMMM') + " " + ganttStartDate.format('YYYY') + " - " + ganttEndDate.format('MMMM') + " " + ganttEndDate.format('YYYY'));
}

function taskGridClick(event) {
    var row = $(event.target).closest('tr');
    var key = row.data("id");

    const url = new URL(window.location);
    url.searchParams.set("header", key);
    history.pushState({}, "", url);

    if (key) {
        openFulfilmentView(key);
    }
}

function taskGridSingleClick(event) {
    var row = $(event.target).closest('tr');
    currentHeaderId = row.data("id");
    if (!currentHeaderId) {
        $("#btnChangeOrders").attr("disabled", "disabled");
        return;
    }

    let record = $("#grid").igGrid("findRecordByKey", currentHeaderId);
    let currentAgreement = record?.AgreementNumber;
    let division = record?.Division;
    let isA = currentAgreement && (currentAgreement[0] === 'A' || currentAgreement[0] === 'a');
    let inDivision = window.userDivisions.includes(division);

    if (isA && inDivision) {
        $("#btnChangeOrders").show();
    } else {
        $("#btnChangeOrders").hide();
    }
}

function assetGridClick(event) {
    var row = $(event.target).closest('tr');
    var key = row.data("id");

    if (key) {
        openNotesView(key, 'asset');
    }
}

var openedHeaderId = 0;

function openFulfilmentView(headerId) {
    openedHeaderId = headerId;
    var url = '/Fulfilment?headerId=' + headerId;
    $('#frameFulfilment').attr('src', url);
    $("#dialogWindow").igDialog("open");
}

function addChangePushState(changeId) {
    if (changeId) {
        currentChangeId = changeId
        const url = new URL(window.location);
        url.searchParams.set("changeId", currentChangeId);
        history.pushState({}, "", url);
    }
}

function removeChangePushState() {
    currentChangeId = null;
    const url = new URL(window.location);
    url.searchParams.delete("changeId");
    history.pushState({}, "", url);
}

function openChangeView() {
    const url = new URL(window.location);
    url.searchParams.set("header", currentHeaderId);
    url.searchParams.set("page", "change");
    if (currentChangeId) {
        url.searchParams.set("changeId", currentChangeId);
    }
    history.pushState({}, "", url);

    var src = '/ChangeOrder/List?headerId=' + currentHeaderId;
    if (currentChangeId) {
        src = '/ChangeOrder/Details?headerId=' + currentHeaderId + '&changeId=' + currentChangeId;
    }
    $('#frameChange').attr('src', src);
    $("#dialogWindowChange").igDialog("open");
}

function refresh() {
    $("#grid").igGrid("dataBind");
}


var gridWidth = "3300";
var cellWidth = "35";


function formatBarAgreement(val) {

    if (val == null) return val;
    var barData = val.split('|');
    var agreement = barData[0];
    var startDate = moment(barData[1].substring(0,10));
    var endDate = moment(barData[2].substring(0, 10));
    var deliveryDate = moment(barData[4].substring(0, 10));
    var collectionDate = moment(barData[5].substring(0, 10));
    var status = barData[3];

    var container = $("<div></div>").css("width", gridWidth+"px");

    var topDate = moment(ganttStartDate);
    for (var m = 0; m < 3; m++) {
        var numDays = topDate.daysInMonth();
        var barDate = moment(topDate);
        for (var d = 0; d < numDays; d++) {
            var cssClass = '';
            var barText = '';
            var itemid = 0;
            if (barDate >= deliveryDate && barDate <= collectionDate) {
                if (status == 0) {
                    cssClass = 'div-danger';
                    barText = 'Unfulfiled';
                }
                if (status == 1) {
                    cssClass = 'div-info';
                    barText = 'Partially fulfiled';
                }
                if (status == 2) {
                    cssClass = 'div-warning';
                    barText = 'Over fulfiled';
                }
                if (status == 3) {
                    cssClass = 'div-success';
                    barText = 'Fully fulfiled';
                }
                itemid = 1;
                barText = agreement + ' ' + formatDateAsLocal(startDate)+"-"+formatDateAsLocal(endDate)+" "+barText;
            }
            
            var cell = $("<div style='float:left;padding:0;height:20px;border:solid 1px #cacaca;position:relative;' class='nowrap gantt-bar-cell'></div>")
                .css("width", cellWidth + "px")
                .css("max-width", cellWidth + "px")
                .addClass(cssClass)
                .attr('title', barText)
                .data("itemid", itemid);

            if (barText) {
                cell.append($("<span class='gantt-text-display'></span>").text(barText));
            }

            container.append(cell);
            barDate = barDate.add(1, "day");
        }
        topDate = topDate.add(1, "month").startOf("month");
    }

    combineGanttCells(container);
    return container.get(0).outerHTML;
}

function refreshAssetEvents() {
    if (isAssetView) {
        assetEventData = {};
        assetEventsCurrent = false;

        var req = {};
        req.divisions = '';
        req.startDate = ganttStartDate.toJSON();
        req.endDate = ganttEndDate.toJSON();

        $.ajax({
            url: "api/Event/Events",
            type: "POST",
            async: false,
            data: JSON.stringify(req),
            contentType: 'application/json; charset=utf-8',
            dataType: 'json',
            success: function (data) {
                assetEventData = data;
                assetEventsCurrent = true;
            }
        });
    }
}

function formatBarAsset(val) {

    if (val == null) return val;
    if (!assetEventsCurrent) {
        return "<div class='assetLoadingRow' data-assetId='" + val + "'>Loading...</div>";
    } else {
        return generateAssetEventsBar(val);
    }
}

function refreshAssetEventsBars(assetId) {
    var pending = $("div.assetLoadingRow");
    for (var i = 0; i < pending.length; i++) {
        var table = pending[i];
        var assetId = table.data('assetId');
        table.empty();
        table.append($(generateAssetEventsBar(assetId)));
    }
}

function generateAssetEventsBar(assetId) {
    var container = $("<div></div>").css("width", gridWidth + "px").css("display", "grid");
    var items = assetEventData.events[assetId];

    if (items != null) {
        // Sort items by itemStartDate
        items.sort((a, b) => moment(a.startDate) - moment(b.startDate));

        items.forEach(item => {
            var itemStartDate = moment(item.startDate);
            var itemEndDate = moment(item.endDate);

            var itemContainer = $("<div></div>").css("width", gridWidth + "px");
            var topDate = moment(ganttStartDate);

            for (var m = 0; m < 3; m++) {
                var numDays = topDate.daysInMonth();
                var barDate = moment(topDate);

                for (var d = 0; d < numDays; d++) {
                    var cssClass = '';
                    var barText = '';
                    var itemid = '';

                    if (barDate <= itemEndDate && itemStartDate <= barDate) {
                        cssClass = item.cssClass;
                        barText = `${item.eventType}: ${item.title} (${itemStartDate.format(dateFormat.toUpperCase())} - ${itemEndDate.format(dateFormat.toUpperCase())})`;
                        itemid = item.title;
                    }

                    var itemDiv = $("<div style='float:left;padding:0;height:20px;border:solid 1px #cacaca;position:relative;' class='nowrap gantt-bar-cell'></div>")
                        .css("width", cellWidth + "px")
                        .css("max-width", cellWidth + "px")
                        .addClass(cssClass)
                        .attr('title', barText)
                        .data("itemid", itemid);

                    if (barText) {
                        itemDiv.append($("<span class='gantt-text-display'></span>").text(barText));
                    }

                    itemContainer.append(itemDiv);
                    barDate = barDate.add(1, "day");
                }

                topDate = topDate.add(1, "month").startOf("month");
            }

            combineGanttCells(itemContainer);
            container.append(itemContainer);
        });
    } else {
        // Handle case where there are no items for the assetId
        var container = $("<div></div>").css("width", gridWidth + "px")
        var topDate = moment(ganttStartDate);
        for (var m = 0; m < 3; m++) {
            var numDays = topDate.daysInMonth();
            var barDate = moment(topDate);

            for (var d = 0; d < numDays; d++) {
                var itemDiv = $("<div style='float:left;padding:0;height:20px;border:solid 1px #cacaca;' class='nowrap'></div>")
                    .css("width", cellWidth + "px")
                    .css("max-width", cellWidth + "px");

                container.append(itemDiv);
                barDate = barDate.add(1, "day");
            }

            topDate = topDate.add(1, "month").startOf("month");
        }
    }

    return container.get(0).outerHTML;
}



function drawGanttHeader() {

    var header = $("#gridGanttHeader");
    if (header != null) {

        header.width(gridWidth + "px");
        var table = $("<table class='table table-light table-fixed table-remove'>").width(gridWidth + "px");

        // Format top line
        var trTop = $("<tr></tr>");
        table.append(trTop);

        var topDate = moment(ganttStartDate);
        var totalDays = 0;
        for (var m = 0; m < 3; m++) {
            var numDays = topDate.daysInMonth();
            trTop.append($("<td style='text-align:center;border-right: solid 1px #cacaca;background:transparent;color:#FFF;'></td>").css('width', (numDays * cellWidth) + "px").attr('colspan', numDays).text(topDate.format('MMMM') + " " + topDate.format('YYYY')));
            topDate = topDate.add(1, "month");
            totalDays = totalDays + numDays;
        }
        var spacerWidth = (gridWidth - (totalDays * cellWidth)) + "px";
        trTop.append($("<td style='background:transparent'>&nbsp;</td>").css("width", spacerWidth)); //spacer

        // Day-of-week letter row (M, T, W, T, F, S, S)
        var dayLetters = ['S', 'M', 'T', 'W', 'T', 'F', 'S'];
        var trDayNames = $("<tr></tr>");
        table.append(trDayNames);
        var dayNameDate = moment(ganttStartDate);
        for (var d = 0; d < totalDays; d++) {
            var dayOfWeek = dayNameDate.day();
            var isWeekend = (dayOfWeek === 0 || dayOfWeek === 6);
            var bgColor = isWeekend ? '#d6d6d6' : '#3a7ca5';
            trDayNames.append($("<td style='padding:0;height:20px;border:solid 1px #cacaca;text-align:center;font-weight:bold;color:#FFF;'></td>").css({ "width": cellWidth + "px", "max-width": cellWidth + "px", "background": bgColor }).text(dayLetters[dayOfWeek]));
            dayNameDate = dayNameDate.add(1, "day");
        }
        trDayNames.append($("<td style='background:transparent;border:none'>&nbsp;</td>").css("width", spacerWidth)); //spacer

        // Day number row
        trDays = $("<tr></tr>");
        table.append(trDays);
        var barDate = moment(ganttStartDate);
        for (var d = 0; d < totalDays; d++) {
            var dayOfWeek = barDate.day();
            var isWeekend = (dayOfWeek === 0 || dayOfWeek === 6);
            var bgColor = isWeekend ? '#d6d6d6' : 'transparent';
            trDays.append($("<td style='padding:0;height:20px;border:solid 1px #cacaca;text-align:center;font-weight:bold;color:#FFF;'></td>").css({ "width": cellWidth + "px", "max-width": cellWidth + "px", "background": bgColor }).text(barDate.date()));
            barDate = barDate.add(1, "day");
        }
        trDays.append($("<td style='background:transparent;border:none'>&nbsp;</td>").css("width", spacerWidth)); //spacer
        header.empty();
        header.append(table);
    }

}

function combineGanttCells(row) {

    var row = $(row);
    var cells = row.find('div');
    var lastCell = null;
    var colspan = 1;

    for (var i = 0; i < cells.length; i++) {
        var thisCell = $(cells[i]);
        if (lastCell == null || lastCell.data('itemid') != thisCell.data('itemid') || thisCell.data('itemid') == 0) {
            lastCell = $(thisCell).addClass('gantt-primary-cell');
            colspan = 1;
        } else {
            thisCell.hide();
            colspan = colspan + 1;
            lastCell.css('width', (cellWidth * colspan) + "px");
            lastCell.css('max-width', (cellWidth * colspan) + "px");
        }
    }
}



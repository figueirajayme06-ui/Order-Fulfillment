function formatFulfilmentStatus(val) {

    if (val == 0) {
        return '<i  title="Unfulfiled" class="bi bi-plus-circle-dotted text-danger">';
    }

    if (val == 1) {
        return '<i title="Partially Fulfilled" class="bi bi-circle-half text-info"></i>';
    }

    if (val == 2) {
        return '<i title="Overfulfiled" class="bi bi-exclamation-diamond text-danger"></i>';
    }

    if (val == 3) {
        return '<i title="Fully fulfiled" class="bi bi-check-circle text-success"></i>';
    }

    return val;
}

function formatEffectiveProbability(val) {
    if (val === null || val === undefined || val === "") {
        return "";
    }

    return val + "%";
}

function formatAssetStatusCompound(val) {

    var status = '';

    if (val == 1 || val == 11 || val == 21) {
        status = status + '<i title="Part of one or more ringfences" class="bi bi-file-earmark-lock"></i>&nbsp;';
    }

    if (val == 20 || val == 21) {
        var indicator = $('<i class="bi bi-exclamation-square"></i>');
        indicator.attr('title', 'This asset has one or more notes. Double click the row to view.');
        indicator.addClass('text-danger');
        status = status + indicator.prop('outerHTML');
    } else {
        var indicator = $('<i class="bi bi-pencil-square"></i>');
        indicator.attr('title', 'Double click the row to add notes to this asset.');
        status = status + indicator.prop('outerHTML');
    }

    return status;
}

function formatARMStatus(val) {

    switch (val) {
        case '0':
            return 'Not fitted';
        case '1':
            return 'Ready for Fitting';
        case '2':
            return 'Fitted - 2G';
        case '3':
            return 'Fitted - 3G/4G';
        case '4':
            return 'Fitted - Sat';
        case '5':
            return 'Needs Inspection';
        default:
            return 'Unknown';
    }
}

function resizeGridToFitWindow(gridId) {
    if (!gridId) {
        return;
    }

    var height = "100%";
    var gridContainer = $(`${gridId}_container`);

    if (window.innerHeight > 0 && gridContainer != null && gridContainer.length > 0) {
        // Incase the height above grid is different on different machines
        var heightAboveGrid = gridContainer.offset().top;
        // Height below never changes
        var heightBelowGrid = 50;
        var heightDifference = heightAboveGrid + heightBelowGrid;
        height = (window.innerHeight - heightDifference) + "px";
    }

    $(gridId).igGrid("option", "height", height);
}
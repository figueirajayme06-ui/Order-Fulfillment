// Setup datepickers
function setupFields() {
    $('*[data-datepicker]').each(function (i, e) {
        var $this = $(e);
        $this.datepicker({ dateFormat: $this.attr("data-datepicker") });
    });
}

// Show if a textbox is different to its original value
function setChanged() {
    $('input[data-original]').each(function (i, e) {
        var $this = $(e);
        if ($this.attr("type") == "checkbox") {
            return;
        }
        var newValue = $this.val();
        var originalValue = $this.attr('data-original');
        if (newValue != originalValue) {
            var message = `Original value is ${originalValue}.`;
            if (!originalValue) {
                message = "No original value.";
            }
            $this.attr("title", message);
            $this.addClass("value-changed")
        } else {
            $this.removeAttr("title");
            $this.removeClass("value-changed")
        }
    });
}

// Set a value to false and show the modal if the form is valid
function setFalse(e, message) {
    $(e)?.siblings("input")?.val("false");

    var form = $(e).closest("form");

    if (!form[0]?.checkValidity()) {
        form[0]?.reportValidity();
        return;
    }

    showLoadingModal(message);
}

// Close the dialog
function closeConfiguratorDialog() {
    currentChangeId = null;
    if ($("#dialogConfigurator").length > 0) {
        $("#dialogConfigurator").igDialog("close");
    }
    $('#frameConfigurator').attr('src', 'about:blank');
}

// Format date to yyyy-mm-dd
function formatDate(d) {
    var month = '' + (d.getMonth() + 1),
        day = '' + d.getDate(),
        year = d.getFullYear();

    if (month.length < 2)
        month = '0' + month;
    if (day.length < 2)
        day = '0' + day;

    return [year, month, day].join('-');
}

// Open the configurator
function openConfiguratorView(agreement, btn) {
    const $form = $(btn).closest('form');

    let generic = $("input[name='GenericItemNumber']", $form).val();
    let quantity = $("input[name='Quantity']", $form).val();
    let attributes = $("input[name='Attributes']", $form).val();
    let onHireDate = $("input[name='OnHireDate']", $form).datepicker("getDate");
    let onHire = formatDate(onHireDate);
    let offHireDate = $("input[name='OffHireDate']", $form).datepicker("getDate");
    let offHire = formatDate(offHireDate);

    var json = getPriceSerialised(generic, offHire, onHire, quantity);
    var suffix = generic ? '' : '/Selection';

    var src = '/Configuration' + suffix +
        '?agreementNumber=' + encodeURIComponent(agreement) +
        '&quantity=' + encodeURIComponent(quantity) +
        '&json=' + encodeURIComponent(json) +
        '&attributes=' + encodeURIComponent(attributes) +
        '&lineId=' + -1;

    $('#frameConfigurator').attr('src', src);
    $("#dialogConfigurator").igDialog("open");
}

let loadingModal = null;

// Show the modal with a message
function showLoadingModal(message) {
    $('#loadingMessage').text(message);
    if (loadingModal == null) {
        loadingModal = new bootstrap.Modal(document.getElementById('loadingModal'));
    }
    loadingModal.show();
}

// Hide the modal with a message
function hideLoadingModal() {
    loadingModal.hide();
}

// Get attributes to map to NOF from Configurator
function getAttributesForNOFFromConfiguratorData(json) {
    var response = {};
    $.ajax({
        url: "/api/Salesforce/AttributesForNOF?json=" + encodeURIComponent(json),
        type: "get",
        async: false,
        success: function (data) {
            response = data;
        }
    });
    return response;
}

// Get attributes to map to NOF from Configurator
function getPricesForProducts(json) {
    var response = {};
    $.ajax({
        url: "/api/Salesforce/PricesForProducts?json=" + encodeURIComponent(json),
        type: "get",
        async: false,
        success: function (data) {
            response = data;
        }
    });
    return response;
}

// Get additional information for header and lines to do pricing
function getAdditionalInformationForHeaderAndLinesForPricing(headerId, changeOrderId) {
    var response = {};
    $.ajax({
        url: "/api/Salesforce/AdditionalInformationForHeaderAndLinesForPricing?headerId=" + headerId + "&changeId=" + changeOrderId,
        type: "get",
        async: false,
        success: function (data) {
            response = data;
        }
    });
    return response;
}

// Callback from the configurator
function configuratorCallback(json) {
    var data = JSON.parse(json)

    if ($("#dialogConfigurator").length > 0) {
        $("#dialogConfigurator").igDialog("close");
    }

    showLoadingModal('Processing product...');

    var $form = $("#editLinesForm");
    $("input[name='GenericItemNumber']", $form).val(data?.product?.configuredGenericCode);
    $("input[name='Quantity']", $form).val(data?.product?.configurationAttributes?.SBQQ__Quantity__c || "1");

    var attributeResponse = getAttributesForNOFFromConfiguratorData(json);
    if (attributeResponse) {
        $("input[name='Attributes']", $form).val(attributeResponse.attributes);
        let $lineAttributes = $("#lineAttributes", $form);
        addAttributes($lineAttributes, attributeResponse.attributes);
    }

    var generic = data?.product?.configuredGenericCode;

    var pricingResponse = getPricesForProducts(json);
    if (pricingResponse) {

        let price = -2;

        if (pricingResponse.lines?.length > 0) {
            price = pricingResponse.lines[0].calculation.listPrice;
        } 

        $("input[name='Price']", $form).val(price);
    }

    setTimeout(function () {
        $(`#saveButton`, $form).prop("disabled", false);
        hideLoadingModal();
    }, 1000);
}

// Add the attrributes in a display format
function addAttributes($lineAttributes, attributes) {
    $lineAttributes.html('');

    var attributeList = attributes?.split(";");
    if (!attributes || attributeList.length == 0) {
        $lineAttributes.append(`<span class="badge bg-primary">None</span>`);
        return;
    }
    for (let i = 0; i < attributeList.length; i++) {
        $lineAttributes.append(`<span class="badge bg-primary">${attributeList[i]}</span> `);
    }
}

// Update the details form from the json data from the table
function updateLineFromTable(container, data) {
    let $lineAttributes = $("#lineAttributes", $(container));

    $(`input:not([type="checkbox"])`, $(container)).val('');
    $(`input:not([type="checkbox"])`, $(container)).attr('data-original', '');

    for (const key in data) {
        if (data.hasOwnProperty(key)) {
            const input = $(`input[name="${key}"]`, $(container));
            if (input) {
                if (input.attr("type") == "checkbox") {
                    input.prop("checked", data[key].current == "true");
                } else {
                    input.val(data[key].current);
                    input.attr("data-original", data[key].original);
                }
            }

            if (key == "Attributes") {
                addAttributes($lineAttributes, data[key].current);
            }
        }
    }

    $(`#saveButton`, $(container)).prop("disabled", false);
}

$(document).ready(function () {
    $("#dialogConfigurator").igDialog({
        height: "95%",
        width: "95%",
        modal: true,
        headerText: "Configurator",
        showMinimizeButton: false,
        showMaximizeButton: false,
        showPinButton: false,
        showCloseButton: false,
        showHeader: true,
        showFooter: false,
        zIndex: 100005,
        state: "closed"
    });

    $('#dialogCloseButton').click(function () {
        window.parent.postMessage("closing " + window.location, "*");
        window.parent.closeChangeDialog();
    });

    $('*[data-add]').click(function (e) {
        var $form = $("#lineForm");
        const onHire = $("#editHeaderForm input[name='OnHireDate']").val();
        const offHire = $("#editHeaderForm input[name='OffHireDate']").val();
        const data = {};
        data["OnHireDate"] = {
            current: onHire,
            original: onHire
        };
        data["OffHireDate"] = {
            current: offHire,
            original: offHire
        };
        data["Attributes"] = {
            current: '',
            original: ''
        };
        data["Delete"] = {
            current: 'false',
            original: 'false'
        };
        data["Quantity"] = {
            current: '1',
            original: '1'
        };
        updateLineFromTable($form, data);
        $(`#saveButton`, $form).prop("disabled", true);
        $form.show();
        setChanged();
    })

    $('*[data-enrich]').click(function (e) {
        e.preventDefault();

        try {
            var $button = $(this);
            showLoadingModal('Getting additional data for order...');

            var headerId = $button.data('headerid');
            var changeOrderId = $button.data('changeorderid');
            var response = getAdditionalInformationForHeaderAndLinesForPricing(headerId, changeOrderId);

            if (response) {
                window.location.reload();
            }
        } catch (e) {
            hideLoadingModal();
        }
    })

    $("input[type='checkbox']").change(function () {
        var isChecked = $(this).prop('checked');
        $(this).val(isChecked);
    });

    $('#table-items tbody tr.line_item_row').click(function () {
        $('#table-items tbody tr.line_item_row').removeClass("table-active")
        $(this).addClass("table-active")
        const inputs = this.querySelectorAll("input[type='hidden']");
        const data = {};

        inputs.forEach(input => {
            var $this = $(input);
            let name = $this.attr('name');
            let cleanName = name.replace(/^Lines\[[^\]]*\]\./, '');
            data[cleanName] = {
                current: $this.val(),
                original: $this.data('original')
            }
        });
        var $form = $("#lineForm");
        updateLineFromTable($form, data);
        $form.show();
        setChanged();
    });

    setupFields();
    setChanged();
});
var ringFenceRows = null;

$(document).ready(function () {


    $('#ringFenceReason').change(function () {
        pf_validateRingfence();
    });

    $(document).on("iggridselectionrowselectionchanged", "#grid", function (evt, ui) {

        ringFenceRows = ui.selectedRows;
        pf_validateRingfence();
    });

    $('#btnCreateRingfence').click(function () {

        pf_doRingFence(false);
    });

    $('#pfLstRingfences').on('change', function () {
        pf_validateRingfence();
    });

    pf_loadRingfences();
});

function pf_validateRingfence() {

    var ringfenceId = $('#pfLstRingfences').val();

    if (ringfenceId > 0) {
        $('#btnCreateRingfence').prop('disabled', false);
    } else {
        $('#btnCreateRingfence').prop('disabled', true);
    }
}

function pf_doRingFence(isDelete) {
    var ringfenceId = $('#pfLstRingfences').val();

    var items = [];
    for (var i = 0; i < ringFenceRows.length; i++) {
        items.push(ringFenceRows[i].id);
    }

    pf_callRingfence(ringfenceId, items, pf_ringfenceCallback);
}

function showWarning(overlappingRingfences, yesCallback, noCallback) {
    const overlappingRingfenceDetails = Array.isArray(overlappingRingfences) ? overlappingRingfences.map(r => {
        const fromDate = moment(r.fromDate)
        const toDate = moment(r.toDate)
        return `
            <strong>Overlapping Ringfence Details:</strong><br>
            <strong>Title:</strong> ${r.title}<br>
            <strong>Ringfence Period:</strong> ${fromDate.format(dateFormat.toUpperCase())} - ${toDate.format(dateFormat.toUpperCase())}<br>
            <strong>Owner:</strong> ${r.owner}<br>
            <strong>Assets:</strong> ${r.assetIds.join(', ')}
        `;
    }).join('<br><hr>') : '';

    $.confirm({
        title: 'Warning!',
        content: `This will create an overlapping ringfence with existing assets.<br>
            <strong>Existing ringfences will <u>not</u> be replaced, but will overlap.</strong><br><hr>
            ${overlappingRingfenceDetails}<br><hr>
            Do you want to proceed?<br>`,
        buttons: {
            confirm: function () {
                yesCallback();
            },
            cancel: function () {
                noCallback();
            }
        }
    });
}


function pf_callRingfence(ringfenceId, items, callback) {
    $('#pf_spinner_ringfence').show();

    var request = {};
    request.ringfenceId = ringfenceId;
    request.assetIds = items;

    $.ajax({
        type: "POST",
        url: "/api/FulfilmentEngine/RingfenceOverlap",
        data: JSON.stringify(request),
        contentType: "application/json",
        success: function (data) {
            $('#pf_spinner_ringfence').hide();
            if (data.isSuccess) {
                if (data.overlappingRingfences.length > 0) {
                    showWarning(data.overlappingRingfences,
                        function () {
                            pf_contineCallRingfence(ringfenceId, items, callback);
                        },
                        function () {
                        }
                    );
                } else {
                    pf_contineCallRingfence(ringfenceId, items, callback);
                }
            } else {
                showError(data.errorMessage);
            }
        },
        error: function (xhr, status, error) {
            $('#pf_spinner_ringfence').hide();
            showError("An error occurred: " + xhr.responseText);
        }
    });
}

function pf_contineCallRingfence(ringfenceId, items, callback) {
    $('#pf_spinner_ringfence').show();

    var request = {};
    request.ringfenceId = ringfenceId;
    request.assetIds = items;

    $.ajax({
        type: "POST",
        url: "/api/FulfilmentEngine/Ringfence",
        data: JSON.stringify(request),
        contentType: "application/json",
        success: function (data) {
            $('#pf_spinner_ringfence').hide();
            if (data.isSuccess) {
                showSuccess('Ringfence successfully updated');
            } else {
                showError(data.errorMessage);
            }
        },
        error: function (xhr, status, error) {
            $('#pf_spinner_ringfence').hide();
            showError("An error occurred: " + xhr.responseText);
        }
    });
}

function pf_ringfenceCallback(response) {

    $('#pf_spinner_ringfence').hide();

    if (response.isSuccess) {

        $('#grid').igGrid('dataBind');

    } else {

        showError("Ringfencing failed: " + response.errorMessage);
    }
}

function pf_loadRingfences() {

    $.ajax({
        url: "/api/FulfilmentEngine/GetRingfences",
        type: "GET",
        async: false,
        success: function (data) {

            $('#pfLstRingfences').empty();
            $('#pfLstRingfences').append('<option value="0">Select a ringfence</option>');

            if (data.ringfences) {
                // Sort the array by title asecending
                let sorted = [...data.ringfences].sort((a, b) => {
                    if (a.title < b.title) return -1;
                    if (a.title > b.title) return 1;
                    return 0;
                });

                for (var i = 0; i < sorted.length; i++) {
                    $('#pfLstRingfences').append('<option value="' + sorted[i].id + '">' + sorted[i].title + '</option>');
                }
            }
        }
    });
}
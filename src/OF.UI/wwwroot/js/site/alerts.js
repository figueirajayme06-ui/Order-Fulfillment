$(document).ready(function () {

    getAlerts();
    setInterval(getAlerts, 60000);
});

function getAlerts() {

    $.ajax({
        url: "api/Task/Alerts",
        type: "get",
        async: false,
        success: function (data) {
            drawAlerts(data);
        }
    });
}

function drawAlerts(data) {

    var alertsIndicator = $('#alertsIndicator');
    var alertsList = $('#alertsList');

    if (data.length == 0) {

        alertsIndicator.text("0");
        alertsList.empty();
        alertsList.append($('<li><a class="dropdown-item" href="#">You have no alerts</a></li>'));
    }
    else {

        var numItems = data.length;
        if (numItems > 99) {
            numItems = '99+';
        }

        alertsIndicator.text(numItems);
        alertsList.empty();
        for (var a = 0; a < data.length; a++) {
            if (a < 20) {
                // Limit to 20 alerts 
                alertsList.append($('<li><a class="dropdown-item" href="javascript:openFulfilmentView(\'' + data[a].headerId + '\',0);">' + data[a].text + '</a></li>'));
            }
        }

    }

}
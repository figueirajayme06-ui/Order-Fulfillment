var usersJSON = [];

function getUsers() {

    if (usersJSON.length == 0) {
        $.ajax({
            url: "api/Task/Users",
            type: "get",
            async: false,
            success: function (data) {
                usersJSON = data;
                usersJSON.unshift({ 'fullName': 'Unassigned', 'loginName': '' });
            }
        });
    }

    return usersJSON;
}

function getUserByLogin(login) {
    for (var i = 0; i < usersJSON.length; i++) {
        if (usersJSON[i].loginName == login) {
            return usersJSON[i].fullName;
        }
    }
}
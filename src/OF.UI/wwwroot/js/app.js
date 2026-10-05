function showSuccess(msg) {
    $.alert(msg, 'Success!')
}

function showSaveSuccess() {
    showSuccess('<strong>Success</strong> Your changes were saved to the database.');
}

function showError(msg) {
    $.alert(msg, 'Error!');
}

function showMessage(msg, title) {
    $.alert(msg, title);
}

function showSaveError() {
    showError('<strong>Error!</strong> Your changes could not be saved. Please check for validation errors and try again.');
}

function showConfirm(title, message, yesCallback, noCallback) {
    $.confirm({
        title: title,
        content: message,
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

const isPositiveInteger = string => {
    const number = Number(string);
    const isInteger = Number.isInteger(number);
    const isPositive = number > 0;

    return isInteger && isPositive;
}
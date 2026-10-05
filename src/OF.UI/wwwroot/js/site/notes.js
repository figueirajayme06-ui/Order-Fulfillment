$("#pnlNotesDialog").igDialog({
    headerText: "Notes",
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

function createNoteTitle(userName, date) {
    return "On " + formatDateAsLocal(date) + " " + userName + " wrote:";
}

function createNewNoteFromTemplate(id, title, note) {
    var newNote = $('#pnlNoteTemplate').clone();
    newNote.attr('id', 'note-item-' + id);
    newNote.data('note-id', id);
    newNote.find('span.note_title').text(title);
    newNote.find('textarea').text(note);

    var deleteButton = newNote.find('button').first();
    deleteButton.data('note-id', id);
    deleteButton.click(function() {
        deleteNote($(this).data('note-id'));
    });

    $('#pnlNotes').prepend(newNote);
    newNote.show();
    return newNote;
}

var addedNotes = [];
$('#btnAddNote').click(function () {
    var id = 0;
    var title = createNoteTitle(userFullName, new Date());
    var note = "";
    var newNote = createNewNoteFromTemplate(id, title, note);
    newNote.find('textarea').attr("readonly", false);
    addedNotes.push(newNote);
});

$('#btnAddNoteSave').click(function () {
    if (!saveNotesChanges()) {
        showError("Your notes could not be saved, please try again.");
    } else {
        window.location.reload();
    }
});

$('#btnAddNoteCancel').click(function () {
    if (addedNotes.length == 0) {
        $("#pnlNotesDialog").igDialog("close");
        if (notes_callback != null) {
            notes_callback();
        }
    } else {
        showConfirm('Warning', 'You have unsaved notes. Close anyway?', function () {
            $("#pnlNotesDialog").igDialog("close");
            if (notes_callback != null) {
                notes_callback();
            }
        }, function () { });
    }
});

function saveNotesChanges() {

    var successCount = 0;
    var tryCount = 0;
    var unsavedNotes = [];

    for (var i = 0; i < addedNotes.length; i++) {

        var note = addedNotes[i];
        if (note != null) {
            var id = note.data('note-id');
            var text = note.find('textarea').val();

            if (id == 0 && text != "") {

                var request = {};
                request.key = notes_key;
                request.noteType = notes_type;
                request.note = text;
                $.ajax({
                    url: "api/Note/CreateNote",
                    type: "put",
                    data: JSON.stringify(request),
                    contentType: "application/json",
                    async: false,
                    success: function (data) {

                        if (data.isSuccess) {
                            addedNotes[i] = null;
                            successCount = successCount + 1;
                            note.data('note-id', data.id);
                            note.find('textarea').attr('readonly', true);
                        } else {
                            unsavedNotes.push(note);
                        }
                    }
                });
                tryCount = tryCount + 1;
            } else {
                unsavedNotes.push(note);
            }
        }
    }

    addedNotes = unsavedNotes;

    if (successCount == tryCount) {
        return true;
    } else {
        return false;
    }
}

function getNoteCount(key, type, callback) {

    $.ajax({
        url: "api/Note/GetNoteCount?key=" + key + "&noteType=" + type,
        type: "get",
        success: function (data) {
            callback(data);
        }
    });
}

function loadNotesData(key, type) {

    $.ajax({
        url: "api/Note/GetNotes?key=" + key + "&noteType=" + type,
        type: "get",
        async: false,
        success: function (data) {
            processNotes(data);
        }
    });
}

var loadedNotes = [];

function processNotes(data) {

    loadedNotes = data;
    for (var i = 0; i < data.length; i++) {

        var title = createNoteTitle(data[i].user.fullName, data[i].note.lastUpdatedDate);
        var newNote = createNewNoteFromTemplate(data[i].note.id, title, data[i].note.note1);
        if (data[i].note.lastUpdatedBy != userLoginName) {
            newNote.find('button').hide();
        }
    }
}
function deleteNote(id) {

    if (id == 0) {

        var item = $('#note-item-' + id);
        item.remove();
        var index = addedNotes.indexOf(item);
        addedNotes.splice(index, 1);

    }
    else {
        showConfirm('Warning', 'Are you sure you want to delete this note?', function () {

            var request = {};
            request.id = id;

            $.ajax({
                url: "api/Note/DeleteNote",
                type: "delete",
                data: JSON.stringify(request),
                contentType: "application/json",
                async: false,
                success: function (data) {
                    if (data.isSuccess) {
                        var item = $('#note-item-' + id);
                        item.remove();
                    }
                }
            });
        }, function () { });
    }
}

var notes_key;
var notes_type;
var notes_callback;

function openNotesView(key, type, callback) {

    loadedNotes = [];
    addedNotes = [];
    notes_key = key;
    notes_type = type;
    notes_callback = callback;
    $('#pnlNotes').empty();
    loadNotesData(key, type);
    $("#pnlNotesDialog").igDialog("open");
}
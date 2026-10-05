$(document).ready(function () {

    var selectedCellText = "";

    function clearGridCopySelection() {
        $("#grid .grid-row-selected, #grid_fixed .grid-row-selected").removeClass("grid-row-selected");
        $("#grid .grid-cell-selected, #grid_fixed .grid-cell-selected").css("background", "").removeClass("grid-cell-selected");
        selectedCellText = "";
    }

    $(document).on("click", "#grid td, #grid_fixed td", function (e) {
        clearGridCopySelection();

        var $cell = $(this);
        var $row = $cell.closest("tr");
        var rowId = $row.attr("data-id");

        if (rowId) {
            $("#grid tr[data-id='" + rowId + "']").addClass("grid-row-selected");
            $("#grid_fixed tr[data-id='" + rowId + "']").addClass("grid-row-selected");
        } else {
            $row.addClass("grid-row-selected");
        }

        $cell.addClass("grid-cell-selected");
        $cell.css("background", "rgba(33, 132, 172, 0.45)");

        selectedCellText = $cell.text().trim();
    });

    $(document).on("keydown", function (e) {
        if ((e.ctrlKey || e.metaKey) && (e.key === "c" || e.key === "C") && !e.shiftKey) {
            if (!selectedCellText) return;

            e.preventDefault();
            e.stopPropagation();

            var textarea = document.createElement("textarea");
            textarea.value = selectedCellText;
            textarea.setAttribute("readonly", "");
            textarea.style.position = "fixed";
            textarea.style.left = "-9999px";
            textarea.style.top = "-9999px";
            textarea.style.opacity = "0";
            document.body.appendChild(textarea);
            textarea.select();

            try {
                document.execCommand("copy");
            } catch (err) {
            }

            document.body.removeChild(textarea);
        }
    });
});

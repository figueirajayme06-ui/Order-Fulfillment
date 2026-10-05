class Fulfilment {
    constructor() {
    }

    drawFulfilmentInfoFromProductAndQuote(configuration) {
        // Get the generic code from the configuration
        let genericCode = configuration.configurationAttributes.genericCode;
        let startDate = configuration.configurationData.quote["On_Hire_Date__c"];
        let endDate = configuration.configurationData.quote["Off_Hire_Date__c"];
        let division = configuration.configurationData.quote["Division__c"];
        let attributes = Object
            .entries(configuration.configurationData.product.configurationAttributes)
            .filter(([key, value]) =>
                value !== null &&
                value !== "" &&
                (typeof value !== 'object') &&
                (typeof value === 'string' && value.toLowerCase() !== "no" && value.toLowerCase() !== "false")
            )
            .map(([key, value]) => `${key}:${value}`);
        this.drawFulfilmentInfo(genericCode, startDate, endDate, division, attributes);

        $('#warehouse-list').on('change', function () {
            var selectedValue = $(this).val(); // Get the selected value

            if (selectedValue === "") {
                // If the first item (default) is selected, show all rows
                $('#fulfilment-list tbody tr').show();
            } else {
                // Otherwise, filter rows by the selected code
                $('#fulfilment-list tbody tr').each(function () {
                    var code = $(this).find('td:first').text().trim(); // Get the code from the first column
                    if (code === selectedValue) {
                        $(this).show(); // Show rows that match
                    } else {
                        $(this).hide(); // Hide rows that don't match
                    }
                });
            }
        });
    }

    drawFulfilmentInfo(genericCode, startDate, endDate, division, attributes) {
        $('#fulfilment_output').empty(); // Clear current output
        $('#fulfilment_spinner').removeClass('d-none'); // Show loading spinner

        // Build the URL with query parameters
        let url = `/api/fulfilment/fulfilmentInfo?genericCode=${genericCode}&startDate=${startDate}&endDate=${endDate}&division=${division}`;

        // Make a POST request with attributes as JSON in the body
        $.ajax({
            url: url,
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(attributes),
            success: function (data) {
                $('#fulfilment_spinner').addClass('d-none'); // Hide the spinner once data is received

                // Warehouse selection
                $('#warehouse-list option:not(:first)').remove();

                // Check if data is valid and contains fulfilment info
                if (data && data.length > 0) {
                    // Create the Bootstrap table
                    let tableHTML = `
                    <table id="fulfilment-list" class="table table-striped table-bordered">
                        <thead>
                            <tr>
                                <th>${window.translator.translate('Warehouse Code')}</th>
                                <th>${window.translator.translate('Warehouse')}</th>
                                <th>${window.translator.translate('Item Number')}</th>
                                <th>${window.translator.translate('Description')}</th>
                                <th>${window.translator.translate('Available')}</th>
                                <th>${window.translator.translate('Count')}</th>
                            </tr>
                        </thead>
                        <tbody>
                `;

                    // Loop through each fulfillment info and build table rows
                    data.forEach(function (fulfilment) {
                        let available = fulfilment.available;
                        let count = fulfilment.count;
                        let availableClass = '';
                        let countClass = '';

                        // Logic for coloring the "available" cell
                        if (available === 0) {
                            availableClass = 'bg-danger'; // Red
                        } else if (available < 3) {
                            availableClass = 'bg-danger'; // Red
                        } else if (available < 5) {
                            availableClass = 'bg-warning'; // Amber
                        } else if (available < count * 0.25) {
                            availableClass = 'bg-danger'; // Red
                        } else if (available < count * 0.50) {
                            availableClass = 'bg-warning'; // Amber
                        } else {
                            availableClass = 'bg-success'; // Green
                        }

                        // Logic for coloring the "count" cell
                        if (count === 0) {
                            countClass = 'bg-danger'; // Red
                        }

                        // Build the table row
                        tableHTML += `
                    <tr>
                        <td>${fulfilment.warehouseCode || ''}</td>
                        <td>${fulfilment.warehouse || ''}</td>
                        <td>${fulfilment.itemNumber || ''}</td>
                        <td>${fulfilment.descriptionIntl || ''}</td>
                        <td class="${availableClass}">${available}</td>
                        <td class="${countClass}">${count}</td>
                    </tr>
                `;
                        var warehouseLoaded = $('#warehouse-list option[value="' + fulfilment.warehouseCode + '"]').length > 0;
                        if (!warehouseLoaded) {
                            var newOption = $('<option>').val(fulfilment.warehouseCode).text(fulfilment.warehouse);
                            $('#warehouse-list').append(newOption);
                        }
                    });

                    // Close the table body and table
                    tableHTML += `
                        </tbody>
                    </table>
                `;

                    // Append the table to the output div
                    $('#fulfilment_output').append(tableHTML);
                } else {
                    // Handle empty data
                    $('#fulfilment_output').append('<p>No fulfilment options are available.</p>');
                }
            },
            error: function (error) {
                $('#fulfilment_spinner').addClass('d-none'); // Hide the spinner in case of an error
                $('#fulfilment_output').append('<p>Error retrieving fulfilment information.</p>');
            }
        });
    }

}

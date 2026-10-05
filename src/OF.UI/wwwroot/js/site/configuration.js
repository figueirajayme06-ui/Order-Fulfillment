function getAttributesAndAdditionalItemsForConfigurator(generic, attributes, quantity) {
    var response = {};
    $.ajax({
        url: `/api/Salesforce/AttributesAndAdditionalItemsForConfigurator?genericCode=${generic}&attributes=${encodeURIComponent(attributes)}&quantity=${quantity}`,
        type: "get",
        async: false,
        success: function (data) {
            response = JSON.parse(data);
        }
    });
    return response;
}

class Configuration {
    constructor(json, loadedCallback, changeCallback) {
        this.configurationData = json;
        this.loadedCallback = loadedCallback;
        this.changeCallback = changeCallback;
    }

    getConfiguration() {

        var frequency = $('#select-Frequency__c');
        var v50 = $('#select-Voltage_50Hz__c');
        var v60 = $('#select-Voltage_60Hz__c');

        if (frequency && v50 && v60) {
            var freq = frequency.val();
            if (freq != null) {
                if (freq.startsWith("50")) {
                    this.configurationData.product.configurationAttributes["Voltage_50Hz__c"] = v50.val();
                    this.configurationData.product.configurationAttributes["Voltage_60Hz__c"] = null;
                } else {
                    this.configurationData.product.configurationAttributes["Voltage_50Hz__c"] = null;
                    this.configurationData.product.configurationAttributes["Voltage_60Hz__c"] = v60.val();
                }
            }
        }
        return this.configurationData;
    }

    drawPicklist(key, field, value, properties) {

        var formFloatingDiv = $('<div>', { class: 'form-floating' });
        var shownValues = null;
        var hiddenValues = null;

        if (field.shownValues && field.shownValues != "") {
            shownValues = field.shownValues.split(/\r?\n/)
        }

        if (field.hiddenValues && field.hiddenValues != "") {
            hiddenValues = field.hiddenValues.split(/\r?\n/)
        }

        // Create the select element
        var selectElement = $('<select>', {
            class: 'form-select product-attribute',
            id: 'select-' + key,
            data: { "toggle": "tooltip", "placement": "top" },
            title: field.fieldMetadata.inlineHelpText ?? '',
            'aria-label': field.fieldMetadata.label ? field.fieldMetadata.label : field.name
        });

        if (!field.required && key != "Frequency__c" && key != "Voltage_50Hz__c" && key != "Voltage_60Hz__c") {
            selectElement.append($('<option>', { text: '-- None --', value: '' }));
        }

        // Append options to the select
        for (var i = 0; i < field.fieldMetadata.picklistValues.length; i++) {
            var plv = field.fieldMetadata.picklistValues[i];
            if (plv.active && plv.value != "Null" && (!shownValues || shownValues.includes(plv.value)) && (!hiddenValues || !hiddenValues.includes(plv.value))) {
                selectElement.append($('<option>', { text: plv.label, value: plv.value, selected: (plv.value == value) || (plv.value == null && plv.defaultValue) }));
            }
        }

        // Value
        selectElement.data('key', key);
        selectElement.data('properties', properties);

        selectElement.on("change", function () {
            var myKey = $(this).data('key');
            var myProps = $(this).data('properties');
            myProps[myKey] = $(this).val();
        });

        // Copy the value back in case we've set a default
        properties[key] = selectElement.val();

        // Create the label element
        var labelElement = $('<label>', { for: 'select-' + key, text: field.fieldMetadata.label ? field.fieldMetadata.label : field.name });

        // Append the select and label to the form-floating div
        formFloatingDiv.append(selectElement);
        formFloatingDiv.append(labelElement);
        var tooltip = new bootstrap.Tooltip(selectElement, {})

        return formFloatingDiv;
    }

    drawCheckbox(key, field, value, properties) {

        var formCheckDiv = $('<div>', { class: 'form-check' });

        // Create the checkbox input element
        var checkboxInput = $('<input>', {
            class: 'form-check-input product-attribute',
            data: { "toggle": "tooltip", "placement": "top" },
            type: 'checkbox',
            title: field.fieldMetadata.inlineHelpText ?? '',
            value: '',
            id: 'check-' + key
        });

        // Value
        checkboxInput.val(value == null ? (field.defaultValue ?? false) : value);

        checkboxInput.data('key', key);
        checkboxInput.data('properties', properties);

        checkboxInput.on("change", function () {
            var myKey = $(this).data('key');
            var myProps = $(this).data('properties');
            myProps[myKey] = $(this).val();
        });

        // Copy the value back in case we've set a default
        properties[key] = checkboxInput.val();

        // Create the label element
        var labelElement = $('<label>', {
            class: 'form-check-label',
            for: 'check-' + key,
            text: field.fieldMetadata.label ? field.fieldMetadata.label : field.name
        });

        // Append the checkbox and label to the form-check div
        formCheckDiv.append(checkboxInput);
        formCheckDiv.append(labelElement);
        var tooltip = new bootstrap.Tooltip(checkboxInput, {})

        return formCheckDiv;
    }

    drawTextArea(key, field, value, properties) {
        var formFloatingDiv = $('<div>', { class: 'form-floating' });

        // Create the textarea element
        var textareaElement = $('<textarea>', {
            class: 'form-control product-attribute',
            data: { "toggle": "tooltip", "placement": "top" },
            placeholder: field.fieldMetadata.inlineHelpText ?? '',
            title: field.fieldMetadata.inlineHelpText ?? '',
            id: 'textarea-' + key,
            maxLength: field.fieldMetadata.length ?? 255,
        });

        // Value
        textareaElement.val(value == null ? (field.defaultValue ?? '') : value);

        // Change event
        textareaElement.data('key', key);
        textareaElement.data('properties', properties);

        textareaElement.on("change", function () {
            var myKey = $(this).data('key');
            var myProps = $(this).data('properties');
            myProps[myKey] = $(this).val();
        });

        // Copy the value back in case we've set a default
        properties[key] = textareaElement.val();

        // Create the label element
        var labelElement = $('<label>', {
            for: 'floatingTextarea',
            text: field.fieldMetadata.label ? field.fieldMetadata.label : field.name
        });

        // Append the textarea and label to the form-floating div
        formFloatingDiv.append(textareaElement);
        formFloatingDiv.append(labelElement);

        var tooltip = new bootstrap.Tooltip(textareaElement, {})

        return formFloatingDiv;
    }


    drawTextBox(key, field, value, properties) {
        var formFloatingDiv = $('<div>', { class: 'form-floating' });

        // Create the textarea element
        var textElement = $('<input>', {
            class: 'form-control product-attribute',
            data: { "toggle": "tooltip", "placement": "top" },
            title: field.fieldMetadata.inlineHelpText ?? '',
            placeholder: field.fieldMetadata.inlineHelpText ?? '',
            id: 'textarea-' + key,
            maxLength: field.fieldMetadata.length ?? 255,
            type: field.fieldMetadata.type != "text" ? "number" : "text"
        });

        // Value
        textElement.val(value == null ? (field.defaultValue ?? '') : value);

        // Change event
        textElement.data('key', key);
        textElement.data('properties', properties);

        // Copy the value back in case we've set a default
        properties[key] = textElement.val();

        textElement.on("change", function () {
            var myKey = $(this).data('key');
            var myProps = $(this).data('properties');
            if ($(this).attr('type') == "number") {
                const result = parseFloat($(this).val());

                // Check if result is a number and not NaN
                if (!isNaN(result)) {
                    myProps[myKey] = result;
                }
            } else {
                myProps[myKey] = $(this).val();
            }
        });

        // Create the label element
        var labelElement = $('<label>', {
            for: 'floatingTextarea',
            text: field.fieldMetadata.label ? field.fieldMetadata.label : field.name
        });

        // Append the textarea and label to the form-floating div
        formFloatingDiv.append(textElement);
        formFloatingDiv.append(labelElement);
        var tooltip = new bootstrap.Tooltip(textElement, {})

        return formFloatingDiv;
    }

    drawFieldByKey(field, key, value, properties) {

        var metadata = field.fieldMetadata;

        if (metadata != null) {

            if (metadata.type == "picklist") {

                return this.drawPicklist(key, field, value, properties);
            }

            if (metadata.type == "boolean") {

                return this.drawCheckbox(key, field, value, properties);
            }

            if (metadata.type == "textarea") {

                return this.drawTextArea(key, field, value, properties);
            }

            return this.drawTextBox(key, field, value, properties);

        } else {
            return null;
        }
    }

    applyVoltageHack() {
        var frequency = $('#select-Frequency__c');
        var v50 = $('#select-Voltage_50Hz__c');
        var v60 = $('#select-Voltage_60Hz__c');

        if (frequency && v50 && v60) {

            var v50 = $('#select-Voltage_50Hz__c');
            var v60 = $('#select-Voltage_60Hz__c');
            frequency.on("change", function () {

                var freq = $(this).val();
                if (freq.startsWith("50")) {
                    v50.parent().parent().show();
                    v60.parent().parent().hide();
                } else {
                    v50.parent().parent().hide();
                    v60.parent().parent().show();
                }
            });

            frequency.trigger("change");
        }
    }

    evaluateFormula(formula, variables) {

        try {
            // Get the variable names as keys and their values
            const variableNames = Object.keys(variables);
            const variableValues = Object.values(variables);

            // Create a function with the variable names as parameters and the formula as the body
            const formulaFunction = new Function(...variableNames, `return ${formula};`);

            // Call the function with the variable values
            return formulaFunction(...variableValues);
        } catch (e) {
            return false;
        }
    }

    executeRule(rule, productOption) {

        var formula = rule.executableFormula;
       
        if (formula && formula.length > 0) {
            var quote = this.configurationData.quote;
            var quoteLine = this.configurationData.readOnly.line;
            var attributes = this.configurationData.product.configurationAttributes
            return this.evaluateFormula(formula, { "quote": quote, "quoteLine": quoteLine, "attributes": attributes, "productOption": productOption });
        }

        return false;
    }

    recalculateSelectionRulesForAllProducts() {
        for (var i = 0; i < this.configurationData.product.optionConfigurations["Services"].length; i++) {
           
            var option = this.configurationData.product.optionConfigurations["Services"][i];
            var productId = option.ProductId;
            var ruleAction = this.evaluateSelectionRulesForProduct(productId, false, option.configurationData);

            if (ruleAction != null) {
                this.takeSelectionActionOnProduct(ruleAction, productId);
            }
        }
    }

    evaluateSelectionRulesForProduct(productId, isLoad, productOption) {

        var rules = this.configurationAttributes.rules;
        if (rules && rules.length > 0) {

            for (var r = 0; r < rules.length; r++) {

                var rule = rules[r];

                if (rule.ruleType == "Selection" && ((isLoad && rule.ruleEvaluationEvent == "Load") || rule.ruleEvaluationEvent == "Always"))  {

                    if (rule.productRule && rule.productRule.productActions) {
                        for (var a = 0; a < rule.productRule.productActions.length; a++) {

                            var action = rule.productRule.productActions[a];

                            if (action.productId == productId) {
                                if (this.executeRule(rule.productRule, productOption)) {
                                    return action.type;
                                }
                            }
                        }
                    }
                }
            }
        }

        return null;
    }

    takeSelectionActionOnProduct(action, productId) {
       
        var checkboxes = $('input.check-' + productId);
        var rows = $('tr.row-' + productId);
        
        switch (action) {
            case "Add":
                checkboxes.prop('checked', true);
                return true;
            case "Disable":
                checcheckboxeskbox.prop('disabled', false);
                return true;
            case "Disable & Remove":
                checkboxes.prop('disabled', true);
                checkboxes.prop('checked', false);
                return true;
            case "Enable & Add":
                checkboxes.prop('disabled', false);
                checkboxes.prop('checked', true);
                return true;
            case "Show & Add":
                rows.show();
                checkboxes.prop('checked', true);
                return true;
            case "Hide & Remove":
                rows.hide();
                checkboxes.prop('checked', false);
                return true;
            default:
                return false;
        }
    }

    drawRelatedList(key, container, configuredOptions, mandatory, skipRules) {

        var table = $('<table>', { class: 'table table-striped table-hover' });
        table.append($('<thead><tr><th scope="col"></th><th scope="col">Description</th><th scope="col">Code</th></tr></thead>'));


        var body = $('<tbody>');
        table.append(body);

        if (configuredOptions == null || configuredOptions.length == 0) {

            body.append($('<tr><td colspan="4">No items to configure</td></tr>'));

        } else {
            for (let i = 0; i < configuredOptions.length; i++) {

                var option = configuredOptions[i];
                var row = $('<tr>');
                var toolCell = $('<td>', { style: 'width:100px' });

                var checkbox = $('<input type="checkbox" class="form-check-input"/>').data('option', option).on("change", function () {
                    var myOption = $(this).data('option');
                    myOption.selected = $(this).is(':checked');
                });
                checkbox.addClass('check-' + option.optionId);

                if (mandatory) {
                    checkbox.prop('disabled', true);
                }

                if (option.selected) {
                    checkbox.prop('checked', true);
                }

                toolCell.append(checkbox);

                var button = $('<button type="button" style="margin-left:3px" class="btn btn-outline-secondary btn-sm"><i class="bi bi-wrench-adjustable"></i></button>');
                toolCell.append(button.data('option-section', key).data('option-product', option).data('configuration', this).on("click", function () {

                    var configuration = $(this).data('configuration');
                    configuration.showOptionsModal($(this).data('option-section'), $(this).data('option-product').ProductId);
                }));

                var buttonInfo = $('<button type="button" style="margin-left:3px" class="btn btn-outline-secondary btn-sm"><i class="bi bi-magic"></i></button>');
                toolCell.append(buttonInfo.data('option-option', option).data('option-product', option).data('configuration', this).on("click", function () {

                    var configuration = $(this).data('configuration');
                    configuration.showAIInfo($(this).data('option-option'), $(this).data('option-product'));
                }));

                row.append(toolCell);

                row.append($('<td>', { text: option.ProductName }));
                row.append($('<td>', { text: option.ProductCode, style: 'width:200px' }));
                row.addClass('row-' + option.optionId);
               
                body.append(row);

                $.get("/api/salesforce/productId?productCode=" + configuredOptions[i].ProductCode, (productIdData) => {

                    var productId = productIdData.id;
                    $('tr.' + 'row-' + configuredOptions[i].optionId).addClass('row-' + productId);
                    $('input.' + 'check-' + configuredOptions[i].optionId).addClass('check-' + productId);
                    configuredOptions[i].ProductId = productId; // store the product id for later use

                    if (!skipRules) {
                        var ruleAction = this.evaluateSelectionRulesForProduct(productId, true, configuredOptions[i].configurationData);

                        if (ruleAction === null && !mandatory) {
                            ruleAction = "Hide & Remove";
                        }

                        this.takeSelectionActionOnProduct(ruleAction, productId);
                    }
                });
            }
        }
        container.append(table);
    }

    drawAttributePanel(container, data, properties, line, isRoot, colNum, callback) {

        if (container != null) {
            var haveProperties = false;

            for (let p = 0; p < data.attributes.length; p++) {

                var field = data.attributes[p];
                if (field.targetField != "attributes") {
                    if (properties.hasOwnProperty(field.targetField) || !isRoot) {

                        if (!isRoot && !properties.hasOwnProperty(field.targetField)) {
                            if (line && line[field.targetField] != null) {
                                properties[field.targetField] = line[field.targetField];
                            } else {
                                if (field.fieldMetadata.defaultValue != null) {
                                    properties[field.targetField] = field.fieldMetadata.defaultValue;
                                }
                            }
                        }

                        const col = $('<div class="col-md-' + (field.fieldMetadata.type == "textarea" ? "12" : colNum) + ' mb-2"></div>');
                        haveProperties = true;

                        // Create the content inside the column
                        const content = this.drawFieldByKey(field, field.targetField, properties[field.targetField], properties);

                        // Append content to the column and add it to the grid
                        col.append(content);

                        if (container.attr("id") != "option-modal-body" && (field.targetField == "Shift_factor__c" || field.targetField == "Number_of_Running_Hours__c" || field.targetField == "RunningCycle__c")) {
                            $('#configuration_extra_props').append(col);
                        } else if (container.attr("id") != "option-modal-body" && (field.fieldMetadata.type == "textarea")) {
                            $('#configuration_extra_props_bottom').append(col);
                        } else {
                            container.append(col);
                        }
                    }
                }
            }

            if (haveProperties) {
                container.closest('tr').prev('tr').find('button').show();
            }
        }

        callback(data);
    }

    attachChangeEvents() {

        $('.product-attribute').on("change", () => {

            this.recalculateSelectionRulesForAllProducts();
            if (this.changeCallback) {
                this.changeCallback();
            }
        });
    }

    drawMainConfiguration(attributeContainer, titleField, quoteLabelField, requiredItemsList, servicesList, recommendedItemsList, additionalItemsList) {

        if (attributeContainer != null) {
            this.attributeContainer = attributeContainer;
            this.titleField = titleField;
            this.quoteLabelField = quoteLabelField;
            this.quoteLabelField.text(this.configurationData.quote.Name);
        }

        var properties = this.configurationData.product.configurationAttributes;
        var genericCode = this.configurationData.product.configuredGenericCode;
        var lineId = this.configurationData.customAttributes.lineId;

        $.get("/api/salesforce/generic?genericCode=" + genericCode + '&lineId' + lineId, (productData) => {
            this.drawAttributePanel(attributeContainer, productData, properties, null, false, 3, (data) => {

                this.configurationAttributes = data;
                if (attributeContainer != null) {
                    this.titleField.text(data.genericCode + ': ' + data.name);
                    this.drawRelatedList("Required Items", requiredItemsList, this.configurationData.product.optionConfigurations["Required Items"], true, false);
                    this.drawRelatedList("Services", servicesList, this.configurationData.product.optionConfigurations["Services"], false, false);
                    this.drawRelatedList("Recommended Items", recommendedItemsList, this.configurationData.product.optionConfigurations["Recommended Items"], false, true);
                    this.drawRelatedList("Additional Items", additionalItemsList, this.configurationData.product.optionConfigurations["Additional Items"], false, true);

                    this.applyVoltageHack();

                    this.attachChangeEvents();


                    $('#option-modal-close').on('click', () => {
                        this.optionModalCancel();
                    });

                    $('#option-modal-cancel').on('click', () => {
                        this.optionModalCancel();
                    });

                    $('#option-modal-save').on('click', () => {
                        this.optionModalSave();
                    });
                }

                if (this.loadedCallback) {
                    this.loadedCallback();
                }
            });
        });
    }

    validatePanel(configuration, attributes, messageContainer) {

        var isValid = true;
        messageContainer.hide();
        messageContainer.text('');
        var rules = configuration.rules;
        if (rules && rules.length > 0) {

            for (var r = 0; r < rules.length; r++) {

                var rule = rules[r];

                if (rule.ruleType == "Validation") {

                    try {
                        if (this.executeRule(rule.productRule, attributes)) {
                            messageContainer.show();
                            isValid = false;
                            messageContainer.text(messageContainer.text() + " " + rule.productRule.errorMessage);
                        }
                    }
                    catch
                    {
                        // If there's un unexpected error, fall through and let Salesforce handle it.
                    }
                }
            }
        }
        
        return isValid;
    }

    showOptionsModal(section, productId) {
        var optionConfigs = this.configurationData.product.optionConfigurations;

        $('#option-modal-error').hide();

        this.savedOptions = JSON.parse(JSON.stringify(optionConfigs));
        var option = null;
        for (var i = 0; i < optionConfigs[section].length; i++) {
            if (optionConfigs[section][i].ProductId == productId) {
                option = optionConfigs[section][i];
                break;
            }
        }

        if (option != null) {
            
            $.get("/api/salesforce/configuration?productId=" + productId, (data) => {
                if (data.attributes && data.attributes.length > 0) {

                    this.optionUnderConfiguration = { "option": option, "data": data };

                    var modalPanel = $('#option-modal-body');
                    modalPanel.empty();
                    this.drawAttributePanel(modalPanel, data, option.configurationData, option.readOnly.line, false, 6, (attdata) => {

                        $('#option-modal-title').text(option.ProductCode + ": " + option.ProductName);
                        $('#option-modal').modal('show');
                    });
                }
                else {
                    showAlert("No attributes", "This product has no attributes to configure.");
                }
            });
        }
    }

    optionModalCancel() {
        this.configurationData.product.optionConfigurations = this.savedOptions;
        $('#option-modal').modal('hide');
    }

    optionModalSave() {
        // Validate
        if (!this.validatePanel(this.optionUnderConfiguration.data, this.optionUnderConfiguration.option.configurationData, $('#option-modal-error'))) {
            return;
        }

        $('#option-modal').modal('hide');
    }

    showAIInfo(option, product) {

        let productName = "";
        let url = "/api/chat/chat?genericCode=" + option.ProductCode + "&parentProduct=" + productName;

        $.get(url, payload, (data) => {
            if (data.attributes && data.attributes.length > 0) {

                var converter = new showdown.Converter();
                var html = converter.makeHtml(converter);

                var modalPanel = $('#chat-output');
                modalPanel.empty();
                modalPanel.html(html);
                $('#chat-modal').modal('show');
            }
            else {
                showAlert("Unavailable", "Sorry. The AI service is currently unavailable.");
            }
        });
    }
}

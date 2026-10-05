class Translator {
    constructor(locale) {
        this.locale = locale;
        this.translations = {
            "en": {
                "Availability Information": "Availability Information",
                "Cancel": "Cancel",
                "Save": "Save",
                "Filter by warehouse": "Filter by warehouse",
                "Loading...": "Loading...",
                "Warehouse Code": "Warehouse Code",
                "Warehouse": "Warehouse",
                "Item Number": "Item Number",
                "Description": "Description",
                "Available": "Available",
                "Count": "Count",
                "No fulfilment options are available.": "No fulfilment options are available.",
                "Error retrieving fulfilment information.": "Error retrieving fulfilment information."
            },
            "fr": {
                "Availability Information": "Informations sur la disponibilité",
                "Cancel": "Annuler",
                "Save": "Enregistrer",
                "Filter by warehouse": "Filtrer par entrepôt",
                "Loading...": "Chargement...",
                "Warehouse Code": "Code d'entrepôt",
                "Warehouse": "Entrepôt",
                "Item Number": "Numéro d'article",
                "Description": "Description",
                "Available": "Disponible",
                "Count": "Nombre",
                "No fulfilment options are available.": "Aucune option de traitement n'est disponible.",
                "Error retrieving fulfilment information.": "Erreur lors de la récupération des informations de traitement."
            },
            "de": {
                "Availability Information": "Verfügbarkeitsinformationen",
                "Cancel": "Abbrechen",
                "Save": "Speichern",
                "Filter by warehouse": "Nach Lager filtern",
                "Loading...": "Laden...",
                "Warehouse Code": "Lagercode",
                "Warehouse": "Lager",
                "Item Number": "Artikelnummer",
                "Description": "Beschreibung",
                "Available": "Verfügbar",
                "Count": "Anzahl",
                "No fulfilment options are available.": "Keine Erfüllungsoptionen verfügbar.",
                "Error retrieving fulfilment information.": "Fehler beim Abrufen der Erfüllungsinformationen."
            },
            "es": {
                "Availability Information": "Información de disponibilidad",
                "Cancel": "Cancelar",
                "Save": "Guardar",
                "Filter by warehouse": "Filtrar por almacén",
                "Loading...": "Cargando...",
                "Warehouse Code": "Código de almacén",
                "Warehouse": "Almacén",
                "Item Number": "Número de artículo",
                "Description": "Descripción",
                "Available": "Disponible",
                "Count": "Cantidad",
                "No fulfilment options are available.": "No hay opciones de cumplimiento disponibles.",
                "Error retrieving fulfilment information.": "Error al recuperar la información de cumplimiento."
            },
            "es_MX": {
                "Availability Information": "Información de disponibilidad",
                "Cancel": "Cancelar",
                "Save": "Guardar",
                "Filter by warehouse": "Filtrar por bodega",
                "Loading...": "Cargando...",
                "Warehouse Code": "Código de bodega",
                "Warehouse": "Bodega",
                "Item Number": "Número de artículo",
                "Description": "Descripción",
                "Available": "Disponible",
                "Count": "Cantidad",
                "No fulfilment options are available.": "No hay opciones de surtido disponibles.",
                "Error retrieving fulfilment information.": "Error al recuperar la información de surtido."
            },
            "pt_BR": {
                "Availability Information": "Informações de disponibilidade",
                "Cancel": "Cancelar",
                "Save": "Salvar",
                "Filter by warehouse": "Filtrar por armazém",
                "Loading...": "Carregando...",
                "Warehouse Code": "Código do armazém",
                "Warehouse": "Armazém",
                "Item Number": "Número do item",
                "Description": "Descrição",
                "Available": "Disponível",
                "Count": "Quantidade",
                "No fulfilment options are available.": "Não há opções de atendimento disponíveis.",
                "Error retrieving fulfilment information.": "Erro ao recuperar informações de atendimento."
            },
            "it": {
                "Availability Information": "Informazioni sulla disponibilità",
                "Cancel": "Annulla",
                "Save": "Salva",
                "Filter by warehouse": "Filtra per magazzino",
                "Loading...": "Caricamento...",
                "Warehouse Code": "Codice magazzino",
                "Warehouse": "Magazzino",
                "Item Number": "Numero articolo",
                "Description": "Descrizione",
                "Available": "Disponibile",
                "Count": "Quantità",
                "No fulfilment options are available.": "Nessuna opzione di evasione disponibile.",
                "Error retrieving fulfilment information.": "Errore nel recupero delle informazioni di evasione."
            },
            "es_CL": {
                "Availability Information": "Información de disponibilidad",
                "Cancel": "Cancelar",
                "Save": "Guardar",
                "Filter by warehouse": "Filtrar por bodega",
                "Loading...": "Cargando...",
                "Warehouse Code": "Código de bodega",
                "Warehouse": "Bodega",
                "Item Number": "Número de artículo",
                "Description": "Descripción",
                "Available": "Disponible",
                "Count": "Cantidad",
                "No fulfilment options are available.": "No hay opciones de despacho disponibles.",
                "Error retrieving fulfilment information.": "Error al recuperar la información de despacho."
            },
            "es_AR": {
                "Availability Information": "Información de disponibilidad",
                "Cancel": "Cancelar",
                "Save": "Guardar",
                "Filter by warehouse": "Filtrar por depósito",
                "Loading...": "Cargando...",
                "Warehouse Code": "Código de depósito",
                "Warehouse": "Depósito",
                "Item Number": "Número de artículo",
                "Description": "Descripción",
                "Available": "Disponible",
                "Count": "Cantidad",
                "No fulfilment options are available.": "No hay opciones de envío disponibles.",
                "Error retrieving fulfilment information.": "Error al recuperar la información de envío."
            },
            "es_PR": {
                "Availability Information": "Información de disponibilidad",
                "Cancel": "Cancelar",
                "Save": "Guardar",
                "Filter by warehouse": "Filtrar por almacén",
                "Loading...": "Cargando...",
                "Warehouse Code": "Código de almacén",
                "Warehouse": "Almacén",
                "Item Number": "Número de artículo",
                "Description": "Descripción",
                "Available": "Disponible",
                "Count": "Cantidad",
                "No fulfilment options are available.": "No hay opciones de despacho disponibles.",
                "Error retrieving fulfilment information.": "Error al recuperar la información de despacho."
            },
            "es_CO": {
                "Availability Information": "Información de disponibilidad",
                "Cancel": "Cancelar",
                "Save": "Guardar",
                "Filter by warehouse": "Filtrar por bodega",
                "Loading...": "Cargando...",
                "Warehouse Code": "Código de bodega",
                "Warehouse": "Bodega",
                "Item Number": "Número de artículo",
                "Description": "Descripción",
                "Available": "Disponible",
                "Count": "Cantidad",
                "No fulfilment options are available.": "No hay opciones de despacho disponibles.",
                "Error retrieving fulfilment information.": "Error al recuperar la información de despacho."
            }
        };
    }

    translate(key) {
        // First try the full locale (e.g., "es_MX")
        if (this.translations[this.locale]?.[key]) {
            return this.translations[this.locale][key];
        }

        // Then try the language only (e.g., "es")
        const language = this.locale.split('_')[0];
        if (this.translations[language]?.[key]) {
            return this.translations[language][key];
        }

        // Finally fallback to English
        return this.translations.en[key] || key;
    }
}
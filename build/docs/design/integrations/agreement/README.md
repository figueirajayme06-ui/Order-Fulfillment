# Agreements & Lines

Agreements are pulled via the RAA messages received from IPG. There are various types of messages received:

* Activation Acknowledgment
* ~~Header Acknowledgement~~ (Unused)
* Header Sync
* Line Acknowledgement
* Live Sync

You can find below some details on the process of managing a processing each of those messages from within its related Azure function.

## Activation Acknowledgment
![High level design](/build/docs/content/.attachments/processes/activation/Function_%20Activation%20Ack.%20BOD.png "Activation Acknowledgment Process")
[Activation BOD Rejected example](/build/docs/content/.attachments/bods/ack-activation-bod-rejected.xml)
[Activation BOD example](/build/docs/content/.attachments/bods/ack-activation-bod.xml)

## Header Sync
![High level design](/build/docs/content/.attachments/processes/activation/Function_%20Header%20Sync%20BOD.png "Header Sync")
[Header Sync BOD example](/build/docs/content/.attachments/bods/sync-header-bod.xml)

## Line Acknowledgement
![High level design](/build/docs/content/.attachments/processes/activation/Function_%20Line%20Ack.%20BOD.png "Line Acknowledgement")
[Line Activation BOD Rejected example](/build/docs/content/.attachments/bods/ack-line-bod-rejected.xml)
[Line Activation BOD example](/build/docs/content/.attachments/bods/ack-line-bod.xml)

## Line Sync
![High level design](/build/docs/content/.attachments/processes/activation/Function_%20Line%20Sync%20BOD.png "Line Sync")
[Line Sync BOD example](/build/docs/content/.attachments/bods/sync-line-bod.xml)
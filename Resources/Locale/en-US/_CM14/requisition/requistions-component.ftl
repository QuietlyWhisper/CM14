# Requisition Computer
requisition-paperwork-receiver-name = USCM Logistics Branch
requisition-paperwork-reward-message = Confirmation Received! Transfered ${$amount} from budget surplus

# Requisition Invoice
requisition-paper-print-name = {$name} invoice
requisition-paper-print-manifest = [head=2]
    {$containerName}[/head][bold]{$content}[/bold][head=2]
    WT. {$weight} LBS
    LOT {$lot}
    S/N {$serialNumber}[/head]
requisition-paper-print-content = - {$count} {$item}